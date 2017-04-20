﻿using System;

using Deveel.Data.Configuration;
using Deveel.Data.Services;
using Deveel.Data.Sql.Expressions;
using Deveel.Data.Sql.Methods;
using Deveel.Data.Sql.Tables;

using Moq;

using Xunit;

namespace Deveel.Data.Sql.Query.Plan {
	public class PlannerTests {
		private IContext context;

		public PlannerTests() {
			var tableInfo = new TableInfo(ObjectName.Parse("sys.tab1"));
			tableInfo.Columns.Add(new ColumnInfo("a", PrimitiveTypes.Integer()));

			var tableManager = new TransientTableManager();
			tableManager.CreateTableAsync(tableInfo).Wait();

			var config = new Configuration.Configuration();
			config.SetValue("currentSchema", "sys");

			var container = new ServiceContainer();
			container.RegisterInstance<IDbObjectManager>(tableManager);

			var mock = new Mock<IContext>();
			mock.SetupGet(x => x.Scope)
				.Returns(container);
			mock.As<IConfigurationScope>()
				.Setup(x => x.Configuration)
				.Returns(config);

			context = mock.Object;

			var info = new SqlFunctionInfo(new ObjectName("count"), PrimitiveTypes.BigInt());
			info.Parameters.Add(new SqlMethodParameterInfo("a", PrimitiveTypes.BigInt()));

			var function = new SqlAggregateFunctionDelegate(info, accumulate => {
				SqlObject r;
				if (accumulate.IsFirst) {
					r = accumulate.Current;
				} else {
					var x = accumulate.Accumulation;
					var y = accumulate.Current;

					r = x.Add(y);
				}

				accumulate.SetResult(r);
			});

			var registry = new SqlMethodRegistry();
			registry.Register(function);

			container.RegisterInstance<IMethodResolver>(registry);
		}

		[Fact]
		public async void PlanSimpleSelect() {
			var query = new SqlQueryExpression();
			query.Items.Add(SqlExpression.Reference(new ObjectName("a")));
			query.From.Table(ObjectName.Parse("sys.tab1"));

			var planner = new DefaultQueryPlanner();
			var node = await planner.PlanAsync(context, new QueryInfo(query));

			Assert.NotNull(node);

			var result = await node.ReduceAsync(context);

			Assert.NotNull(result);
			Assert.Equal(1, result.TableInfo.Columns.Count);
			Assert.Equal(0, result.RowCount);
		}

		[Fact]
		public async void PlanGroupBy() {
			var query = new SqlQueryExpression();
			query.Items.Add(SqlExpression.Function(new ObjectName("Count"),
				new InvokeArgument(SqlExpression.Reference(new ObjectName("*")))));
			query.From.Table(ObjectName.Parse("sys.tab1"));
			query.GroupBy.Add(SqlExpression.Reference(new ObjectName("a")));

			var planner = new DefaultQueryPlanner();
			var node = await planner.PlanAsync(context, new QueryInfo(query));

			Assert.NotNull(node);
		}
	}
}