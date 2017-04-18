﻿using System;
using System.Collections.Generic;
using System.Linq;

using Deveel.Data.Sql;
using Deveel.Data.Sql.Expressions;

using DryIoc;

namespace Deveel.Data.Query.Plan {
	class TableSetPlan {
		private List<TablePlan> tables;
		private bool hasJoins;

		private static Random markerRandomizer = new Random();

		public TableSetPlan() {
			tables = new List<TablePlan>();
			hasJoins = false;
		}

		private const byte SafeJoin = 0;
		private const byte LeftJoinClash = 2;
		private const byte RightJoinClash = 1;

		private static int CanNaturallyJoin(TablePlan plan1, TablePlan plan2) {
			if (plan1.LeftPlan == plan2 || plan1.RightPlan == plan2) {
				return SafeJoin;
			} else if (plan1.LeftPlan != null && plan2.LeftPlan != null) {
				// This is a left clash
				return LeftJoinClash;
			} else if (plan1.RightPlan != null && plan2.RightPlan != null) {
				// This is a right clash
				return 1;
			} else if ((plan1.LeftPlan == null && plan2.RightPlan == null) ||
			           (plan1.RightPlan == null && plan2.LeftPlan == null)) {
				// This means a merge between the plans is fine
				return SafeJoin;
			} else {
				// Must be a left and right clash
				return LeftJoinClash;
			}
		}

		public void AddTablePlan(TablePlan plan) {
			tables.Add(plan);
			hasJoins = true;
		}

		public TablePlan FindTablePlan(ObjectName columnName) {
			if (tables.Count == 1)
				return tables[0];

			var plan = tables.FirstOrDefault(x => x.ContainsColumn(columnName));
			if (plan == null)
				throw new InvalidOperationException($"Unable to find any table that references {columnName}");

			return plan;
		}

		public TablePlan FindCommonTablePlan(ObjectName[] columnNames) {
			if (columnNames.Length == 0)
				return null;

			var plan1 = FindTablePlan(columnNames[0]);

			for (int i = 1; i < columnNames.Length; i++) {
				var plan2 = FindTablePlan(columnNames[i]);
				if (plan2 != plan1)
					return null;
			}

			return plan1;
		}

		public TablePlan FindTableWithUniqueKey(string uniqueKey) {
			var plan = tables.FirstOrDefault(x => x.ContainsUniqueName(uniqueKey));
			if (plan == null)
				throw new InvalidOperationException($"Unable to find any table plan referenced by key {uniqueKey}");

			return plan;
		}

		public int IndexOfPlan(TablePlan plan) {
			return tables.FindIndex(x => x.Equals(plan));
		}

		public TablePlan GetSinglePlan() {
			if (tables.Count != 1)
				throw new InvalidOperationException("The set has not a single table");

			return tables[0];
		}

		public void SetCache() {
			foreach (var table in tables) {
				if (!(table.Plan is CacheNode))
					table.Plan = new CacheNode(table.Plan);
			}
		}

		private TablePlan JoinAllPlans(IList<TablePlan> plans) {
			// If there are no plans then return null
			if (plans.Count == 0) {
				return null;
			}
			// Return early if there is only 1 table.
			else if (plans.Count == 1) {
				return plans[0];
			}

			// Make a working copy of the plan list.
			var workingPlans = new List<TablePlan>(plans);

			// We go through each plan in turn.
			while (workingPlans.Count > 1) {
				var left_plan = workingPlans[0];
				var right_plan = workingPlans[1];
				// First we need to determine if the left and right plan can be
				// naturally joined.
				int status = CanNaturallyJoin(left_plan, right_plan);
				if (status == 0) {
					// Yes they can so join them
					var new_plan = NaturallyJoinPlans(left_plan, right_plan);
					// Remove the left and right plan from the list and add the new plan
					workingPlans.Remove(left_plan);
					workingPlans.Remove(right_plan);
					workingPlans.Insert(0, new_plan);
				} else if (status == 1) {
					// No we can't because of a right join clash, so we join the left
					// plan right in hopes of resolving the clash.
					var new_plan = NaturallyJoinPlans(left_plan, left_plan.RightPlan);
					workingPlans.Remove(left_plan);
					workingPlans.Remove(left_plan.RightPlan);
					workingPlans.Insert(0, new_plan);
				} else if (status == 2) {
					// No we can't because of a left join clash, so we join the left
					// plan left in hopes of resolving the clash.
					var new_plan = NaturallyJoinPlans(left_plan, left_plan.LeftPlan);
					workingPlans.Remove(left_plan);
					workingPlans.Remove(left_plan.LeftPlan);
					workingPlans.Insert(0, new_plan);
				} else {
					throw new InvalidOperationException();
				}
			}

			// Return the working plan of the merged tables.
			return workingPlans[0];
		}

		private static String CreateRandomOuterJoinName() {
			var v1 = markerRandomizer.Next();
			var v2 = markerRandomizer.Next();
			return $"OUTER_JOIN_{v1:x8}:{v2:X8}";
		}

		private TablePlan NaturallyJoinPlans(TablePlan plan1, TablePlan plan2) {
			JoinType joinType;
			SqlExpression onExpression;
			TablePlan leftPlan, rightPlan;

			// Are the plans linked by common join information?
			if (plan1.RightPlan == plan2) {
				joinType = plan1.RightJoinType;
				onExpression = plan1.RightOnExpression;
				leftPlan = plan1;
				rightPlan = plan2;
			} else if (plan1.LeftPlan == plan2) {
				joinType = plan1.LeftJoinType;
				onExpression = plan1.LeftOnExpression;
				leftPlan = plan2;
				rightPlan = plan1;
			} else {
				// Assertion - make sure no join clashes!
				if ((plan1.LeftPlan != null && plan2.LeftPlan != null) ||
				    (plan1.RightPlan != null && plan2.RightPlan != null)) {
					throw new InvalidOperationException(
						"Assertion failed - plans can not be naturally join because " +
						"the left/right join plans clash.");
				}

				// Else we must assume a non-dependant join (not an outer join).
				// Perform a natural join
				var node = new NaturalJoinNode(plan1.Plan, plan2.Plan);
				return MergeTables(plan1, plan2, node);
			}

			{
				// This means plan1 and plan2 are linked by a common join and ON
				// expression which we evaluate now.
				String outerJoinName;
				switch (joinType) {
					case JoinType.Left:
						outerJoinName = CreateRandomOuterJoinName();
						// Mark the left plan
						leftPlan.UpdatePlan(new CacheMarkNode(leftPlan.Plan, outerJoinName));
						break;
					case JoinType.Right:
						outerJoinName = CreateRandomOuterJoinName();
						// Mark the right plan
						rightPlan.UpdatePlan(new CacheMarkNode(rightPlan.Plan, outerJoinName));
						break;
					case JoinType.Inner:
						// Inner join with ON expression
						outerJoinName = null;
						break;
					default:
						throw new InvalidOperationException($"Join type ({joinType}) is not supported.");
				}

				// Make a Planner object for joining these plans.
				var planner = new TableSetPlan();
				planner.AddTablePlan(leftPlan.Copy());
				planner.AddTablePlan(rightPlan.Copy());

				// Evaluate the on expression
				var node = planner.LogicalEvaluate(onExpression);
				// If outer join add the left outer join node
				if (outerJoinName != null) {
					node = new LeftOuterJoinNode(node, outerJoinName);
				}

				// And merge the plans in this set with the new node.
				return MergeTables(plan1, plan2, node);
			}
		}

		public TablePlan MergeTables(TablePlan left, TablePlan right, IQueryPlanNode mergePlan) {
			// Remove the sources from the table list.
			tables.Remove(left);
			tables.Remove(right);

			// Add the concatination of the left and right tables.
			var concatPlan = ConcatTables(left, right, mergePlan);
			concatPlan.MergeBetween(left, right);
			concatPlan.MarkAsUpdated();
			AddTablePlan(concatPlan);

			return concatPlan;
		}

		public static TablePlan ConcatTables(TablePlan left, TablePlan right, IQueryPlanNode plan) {
			// Merge the column name list
			var newColumnNames = new ObjectName[left.ColumnNames.Length + right.ColumnNames.Length];
			Array.Copy(left.ColumnNames, 0, newColumnNames, 0, left.ColumnNames.Length);
			Array.Copy(right.ColumnNames, 0, newColumnNames, left.ColumnNames.Length, right.ColumnNames.Length);

			// Merge the unique table names list
			var newUniqueNames = new string[left.UniqueNames.Length + right.UniqueNames.Length];
			Array.Copy(left.UniqueNames, 0, newUniqueNames, 0, left.UniqueNames.Length);
			Array.Copy(right.UniqueNames, 0, newUniqueNames, left.UniqueNames.Length, right.UniqueNames.Length);

			// Return the new table source plan.
			return new TablePlan(plan, newColumnNames, newUniqueNames);
		}

		private IQueryPlanNode LogicalEvaluate(SqlExpression expression) {
			if (expression != null)
				// Plan the expression
				PlanForExpression(expression);

			// Naturally join any straggling tables
			NaturalJoinAll();

			// Return the plan
			return GetSinglePlan().Plan;
		}

		private TablePlan NaturalJoinAll() {
			int sz = tables.Count;
			if (sz == 1)
				return tables[0];

			// Produce a plan that naturally joins all tables.
			return JoinAllPlans(tables);
		}

		private void PlanForExpression(SqlExpression expression) {
			if (expression == null)
				return;

			if (expression.ExpressionType.IsLogical()) {
				var binary = (SqlBinaryExpression) expression;
				var op = binary.ExpressionType;

				if (op == SqlExpressionType.Or) {
					// If we are an 'or' then evaluate left and right and union the
					// result.

					// Before we branch set cache points.
					SetCache();

					// Make copies of the left and right planner
					var leftPlanner = Copy();
					var rightPlanner = Copy();

					// Plan the left and right side of the OR
					leftPlanner.PlanForExpression(binary.Left);
					rightPlanner.PlanForExpression(binary.Right);

					// Fix the left and right planner so that they represent the same
					// 'group'.
					// The current implementation naturally joins all sources if the
					// number of sources is different than the original size.
					int leftCount = leftPlanner.tables.Count;
					int rightCount = rightPlanner.tables.Count;
					if (leftCount != rightCount || 
						leftPlanner.hasJoins ||
					    rightPlanner.hasJoins) {
						// Naturally join all in the left and right plan
						leftPlanner.NaturalJoinAll();
						rightPlanner.NaturalJoinAll();
					}

					// Union all table sources, but only if they have changed.
					var leftTables = leftPlanner.tables;
					var rightTables = rightPlanner.tables;
					int sz = leftTables.Count;

					// First we must determine the plans that need to be joined in the
					// left and right plan.
					var leftJoins = new List<TablePlan>();
					var rightJoins = new List<TablePlan>();

					for (int i = 0; i < sz; ++i) {
						var leftPlan = leftTables[i];
						var rightPlan = rightTables[0];
						if (leftPlan.IsUpdated || rightPlan.IsUpdated) {
							leftJoins.Add(leftPlan);
							rightJoins.Add(rightPlan);
						}
					}

					// Make sure the plans are joined in the left and right planners
					leftPlanner.JoinAllPlans(leftJoins);
					rightPlanner.JoinAllPlans(rightJoins);

					// Since the planner lists may have changed we update them here.
					leftTables = leftPlanner.tables;
					rightTables = rightPlanner.tables;
					sz = leftTables.Count;

					var newTableList = new List<TablePlan>(sz);

					for (int i = 0; i < sz; ++i) {
						var leftPlan = leftTables[i];
						var rightPlan = rightTables[i];

						TablePlan newPlan;

						// If left and right plan updated so we need to union them
						if (leftPlan.IsUpdated || rightPlan.IsUpdated) {

							// In many causes, the left and right branches will contain
							//   identical branches that would best be optimized out.

							// Take the left plan, add the logical union to it, and make it
							// the plan for this.
							var node = new LogicalUnionNode(leftPlan.Plan, rightPlan.Plan);

							// Update the plan in this table list
							leftPlan.UpdatePlan(node);

							newPlan = leftPlan;
						} else {
							// If the left and right plan didn't update, then use the
							// left plan (it doesn't matter if we use left or right because
							// they are the same).
							newPlan = leftPlan;
						}

						// Add the left plan to the new table list we are creating
						newTableList.Add(newPlan);
					}

					// Set the new table list
					tables = newTableList;
				} else if (op == SqlExpressionType.And) {
					PlanForExpressions(binary.Left, binary.Right);
				} else {
					throw new InvalidOperationException($"Expression {op} is not a valid logical operation");
				}
			} else {
				// Not a logical expression so just plan for this single expression.
				PlanForExpressions(expression);
			}
		}

		private void PlanForExpressions(params SqlExpression[] expressions) {
			var subLogicExpressions = new List<SqlBinaryExpression>();
			// The list of expressions that have a sub-select in them.
			var subQueryExpressions = new List<SqlExpression>();
			// The list of all constant expressions ( true = true )
			var constants = new List<SqlExpression>();
			// The list of pattern matching expressions (eg. 't LIKE 'a%')
			var patternExpressions = new List<SqlStringMatchExpression>();
			// The list of all expressions that are a single variable on one
			// side, a conditional operator, and a constant on the other side.
			var singleRefs = new List<SqlExpression>();
			// The list of multi variable expressions (possible joins)
			var multiRefs = new List<SqlExpression>();


			foreach (var expression in expressions) {
				SqlExpressionType op;
				var exp = expression;

				if ((expression.ExpressionType.IsBinary() &&
				     expression.ExpressionType.IsMathematical()) ||
				    (!expression.ExpressionType.IsUnary() && 
					!expression.ExpressionType.IsBinary())) {
					exp = SqlExpression.And(exp, SqlExpression.Constant(SqlObject.Boolean(true)));
					op = SqlExpressionType.And;
				} else {
					op = expression.ExpressionType;
				}

				// If the last is logical (eg. AND, OR) then we must process the
				// sub logic expression
				if (op.IsLogical()) {
					subLogicExpressions.Add((SqlBinaryExpression)exp);
				}
				// Does the expression have a sub-query?  (eg. Another select
				//   statement somewhere in it)
				else if (exp.HasSubQuery()) {
					subQueryExpressions.Add(exp);
				} else if (op.IsPattern()) {
					patternExpressions.Add((SqlStringMatchExpression) exp);
				} else {
					// The list of variables in the expression.
					var vars = exp.DiscoverReferences().ToList();
					if (vars.Count == 0) {
						// These are ( 54 + 9 = 9 ), ( "z" > "a" ), ( 9.01 - 2 ), etc
						constants.Add(exp);
					} else if (vars.Count == 1) {
						// These are ( id = 90 ), ( 'a' < number ), etc
						singleRefs.Add(exp);
					} else if (vars.Count > 1) {
						// These are ( id = part_id ),
						// ( cost_of + value_of < sold_at ), ( id = part_id - 10 )
						multiRefs.Add(exp);
					} else {
						throw new InvalidOperationException();
					}
				}
			}

			// The order in which expression are evaluated,
			// (ExpressionPlan)
			var evaluate_order = new List<ExpressionPlan>();

			// Evaluate the constants.  These should always be evaluated first
			// because they always evaluate to either true or false or null.
			EvaluateConstants(constants, evaluate_order);

			// Evaluate the singles.  If formed well these can be evaluated
			// using fast indices.  eg. (a > 9 - 3) is more optimal than
			// (a + 3 > 9).
			EvaluateSingles(singleRefs, evaluate_order);

			// Evaluate the pattern operators.  Note that some patterns can be
			// optimized better than others, but currently we keep this near the
			// middle of our evaluation sequence.
			EvaluatePatterns(patternExpressions, evaluate_order);

			// Evaluate the sub-queries.  These are queries of the form,
			// (a IN ( SELECT ... )), (a = ( SELECT ... ) = ( SELECT ... )), etc.
			EvaluateSubQueries(subQueryExpressions, evaluate_order);

			// Evaluate multiple variable expressions.  It's possible these are
			// joins.
			EvaluateMultiples(multiRefs, evaluate_order);

			// Lastly evaluate the sub-logic expressions.  These expressions are
			// OR type expressions.
			EvaluateSubLogic(subLogicExpressions, evaluate_order);



			// Sort the evaluation list by how optimizable the expressions are,
			evaluate_order.Sort();

			foreach (var plan in evaluate_order) {
				plan.AddToPlan(this);
			}
		}

		private void EvaluateMultiples(List<SqlExpression> multiRefs, List<ExpressionPlan> evaluateOrder) {
			// FUTURE OPTIMIZATION:
			//   This join order planner is a little primitive in design.  It orders
			//   optimizable joins first and least optimizable last, but does not
			//   take into account other factors that we could use to optimize
			//   joins in the future.

			// For each single variable expression
			foreach (var expr in multiRefs) {
				if (!(expr is SqlBinaryExpression))
					throw new InvalidOperationException("Something went wrong in expression build");

				var binary = (SqlBinaryExpression) expr;
				
				// Get the list of variables in the left hand and right hand side
				var lhs_v = (binary.Left as SqlReferenceExpression)?.ReferenceName;
				var rhs_v = (binary.Right as SqlReferenceExpression)?.ReferenceName;

				// Work out how optimizable the join is.
				// The calculation is as follows;
				// a) If both the lhs and rhs are a single variable then the
				//    optimizable value is set to 0.6f.
				// b) If only one of lhs or rhs is a single variable then the
				//    optimizable value is set to 0.64f.
				// c) Otherwise it is set to 0.68f (exhaustive select guarenteed).

				ExpressionPlan exp_plan;
				if (lhs_v == null && rhs_v == null) {
					// Neither lhs or rhs are single vars
					exp_plan = new ExhaustiveJoinExpressionPlan((SqlBinaryExpression) expr, 0.68f);
				} else if (lhs_v != null && rhs_v != null) {
					// Both lhs and rhs are a single var (most optimizable type of
					// join).
					exp_plan = new StandardJoinExpressionPlan((SqlBinaryExpression)expr, 0.60f);
				} else {
					// Either lhs or rhs is a single var
					exp_plan = new StandardJoinExpressionPlan( (SqlBinaryExpression) expr, 0.64f);
				}

				evaluateOrder.Add(exp_plan);
			}
		}

		private void EvaluateSubQueries(List<SqlExpression> subQueryExpressions, List<ExpressionPlan> evaluateOrder) {
			// For each sub-query expression
			foreach (var expression in subQueryExpressions) {
				bool exhaustive;
				ObjectName leftRef = null;
				IQueryPlanNode rightPlan = null;

				// Is this an easy sub-query?
				if (expression.ExpressionType.IsQuantify()) {
					var quantify = (SqlQuantifyExpression) expression;
					var exps = new[]{quantify.Expression.Left, quantify.Expression.Right};
					// Check that the left is a simple enough variable reference
					leftRef = exps[0].AsReference();

					if (leftRef != null) {
						// Check that the right is a sub-query plan.
						rightPlan = exps[1].AsQueryPlanNode();
						if (rightPlan != null) {
							// Finally, check if the plan is correlated or not
							var cv = rightPlan.DiscoverCorrelatedReferences(1);

							if (cv.Count == 0) {
								// No correlated variables so we are a standard, non-correlated
								// query!
								exhaustive = false;
							} else {
								exhaustive = true;
							}
						} else {
							exhaustive = true;
						}
					} else {
						exhaustive = true;
					}
				} else {
					// Must be an exhaustive sub-query
					exhaustive = true;
				}

				// If this is an exhaustive operation,
				if (exhaustive) {
					// This expression could involve multiple variables, so we may need
					// to join.
					var allRefs = expression.DiscoverReferences();

					// Also find all correlated variables.
					var allCorrelated = expression.DiscoverCorrelatedReferences(0);
					int sz = allCorrelated.Count;

					// If there are no variables (and no correlated variables) then this
					// must be a constant select, For example, 3 in ( select ... )
					if (allRefs.Count == 0 && sz == 0) {
						evaluateOrder.Add(new ConstantExpressionPlan(expression));
					} else {
						foreach (var cv in allCorrelated) {
							allRefs.Add(cv.Reference);
						}

						// An exhaustive expression plan which might require a join or a
						// slow correlated search.  This should be evaluated after the
						// multiple variables are processed.
						evaluateOrder.Add(new ExhaustiveSubQueryExpressionPlan(allRefs, expression, 0.85f));
					}
				} else {
					// This is a simple sub-query expression plan with a single LHS
					// variable and a single RHS sub-query.
					evaluateOrder.Add(new SimpleSubQueryExpressionPlan(expression, 0.3f));
				}
			}
		}

		private void EvaluateSingles(List<SqlExpression> singleRefs, List<ExpressionPlan> evaluateOrder) {
			throw new NotImplementedException();
		}

		private void EvaluateConstants(List<SqlExpression> constants, List<ExpressionPlan> evaluateOrder) {
			throw new NotImplementedException();
		}

		private void EvaluateSubLogic(List<SqlBinaryExpression> subLogicExpressions, List<ExpressionPlan> evaluateOrder) {
			each_logic_expr:
			foreach (var expr in subLogicExpressions) {
				// Break the expression down to a list of OR expressions,
				var or_exprs = new[] {expr.Left, expr.Right};

				// An optimizations here;

				// If all the expressions we are ORing together are in the same table
				// then we should execute them before the joins, otherwise they
				// should go after the joins.

				// The reason for this is because if we can lesson the amount of work a
				// join has to do then we should.  The actual time it takes to perform
				// an OR search shouldn't change if it is before or after the joins.

				TablePlan common = null;

				foreach (var or_expr in or_exprs) {
					var vars = or_expr.DiscoverReferences().ToArray();
					// If there are no variables then don't bother with this expression
					if (vars.Length > 0) {
						// Find the common table source (if any)
						var ts = FindCommonTablePlan(vars);
						var or_after_joins = false;
						if (ts == null) {
							// No common table, so OR after the joins
							or_after_joins = true;
						} else if (common == null) {
							common = ts;
						} else if (common != ts) {
							// No common table with the vars in this OR list so do this OR
							// after the joins.
							or_after_joins = true;
						}

						if (or_after_joins) {
							var exp_plan = new SubLogicExpressionPlan(expr, 0.70f);
							evaluateOrder.Add(exp_plan);
							// Continue to the next logic expression
							goto each_logic_expr;
						}
					}
				}
				{
					// Either we found a common table or there are no variables in the OR.
					// Either way we should evaluate this after the join.
					var exp_plan = new SubLogicExpressionPlan(expr, 0.58f);
					evaluateOrder.Add(exp_plan);
				}
			}
		}

		private void EvaluatePatterns(List<SqlStringMatchExpression> patternExpressions, List<ExpressionPlan> evaluateOrder) {
			throw new NotImplementedException();
		}

		private TableSetPlan Copy() {
			var copy = new TableSetPlan();
			copy.tables = tables.Select(x => x.Copy()).ToList();

			// Copy the left and right links in the PlanTableSource
			for (int i = 0; i < tables.Count; i++) {
				var src = tables[i];
				var mod = copy.tables[i];
				// See how the left plan links to which index,
				if (src.LeftPlan != null) {
					int n = IndexOfPlan(src.LeftPlan);
					mod.SetLeftJoin(copy.tables[n], src.LeftJoinType, src.LeftOnExpression);
				}
				// See how the right plan links to which index,
				if (src.RightPlan != null) {
					int n = IndexOfPlan(src.RightPlan);
					mod.SetRightJoin(copy.tables[n], src.RightJoinType, src.RightOnExpression);
				}
			}

			return copy;
		}
	}
}