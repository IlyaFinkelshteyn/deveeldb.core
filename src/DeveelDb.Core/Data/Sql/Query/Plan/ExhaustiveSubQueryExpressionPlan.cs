﻿using System;
using System.Collections.Generic;

using Deveel.Data.Sql.Expressions;

namespace Deveel.Data.Sql.Query.Plan {
	class ExhaustiveSubQueryExpressionPlan : ExpressionPlan {
		public ExhaustiveSubQueryExpressionPlan(IEnumerable<ObjectName> references, SqlExpression expression, float optimizeFactor)
			: base(optimizeFactor) {
			References = references;
			Expression = expression;
		}

		public IEnumerable<ObjectName> References { get; }

		public SqlExpression Expression { get; }

		public override void AddToPlan(TableSetPlan plan) {
			var tablePlan = plan.JoinAllPlansWithReferences(References);
			tablePlan.UpdatePlan(new FullSelectNode(tablePlan.Plan, Expression));
		}
	}
}