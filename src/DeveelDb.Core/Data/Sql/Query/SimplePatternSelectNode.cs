﻿// 
//  Copyright 2010-2017 Deveel
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//


using System;
using System.Threading.Tasks;

using Deveel.Data.Sql.Expressions;
using Deveel.Data.Sql.Tables;

namespace Deveel.Data.Sql.Query {
	public sealed class SimplePatternSelectNode : SingleQueryPlanNode {
		public SimplePatternSelectNode(IQueryPlanNode child, ObjectName columnName, SqlExpressionType op, SqlExpression pattern, SqlExpression escape)
			: base(child) {
			Column = columnName;
			Operator = op;
			Pattern = pattern;
			Escape = escape;
		}

		public ObjectName Column { get; }

		public SqlExpressionType Operator { get; }

		public SqlExpression Pattern { get; }

		public SqlExpression Escape { get; }

		public override async Task<ITable> ReduceAsync(IContext context) {
			var table = await Child.ReduceAsync(context);

			return await table.SearchAsync(context, Column, Operator, Pattern, Escape);
		}
	}
}