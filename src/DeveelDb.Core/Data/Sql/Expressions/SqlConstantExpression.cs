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
using System.Linq;

using Deveel.Data.Serialization;

namespace Deveel.Data.Sql.Expressions {
	public sealed class SqlConstantExpression : SqlExpression {
		internal SqlConstantExpression(SqlObject value) 
			: base(SqlExpressionType.Constant) {
			if (value == null)
				throw new ArgumentNullException(nameof(value));

			Value = value;
		}

		private SqlConstantExpression(SerializationInfo info)
			: base(info) {
			Value = info.GetValue<SqlObject>("value");
		}

		public SqlObject Value { get; }

		public override bool IsReference {
			get {
				if (Value.Type is SqlArrayType) {
					var array = (SqlArray) Value.Value;
					if (array.Any(item => item.IsReference)) {
						return false;
					}
				}

				return false;
			}
		}

		public override SqlType GetSqlType(IContext context) {
			return Value.Type;
		}

		public override SqlExpression Accept(SqlExpressionVisitor visitor) {
			return visitor.VisitConstant(this);
		}

		protected override void AppendTo(SqlStringBuilder builder) {
			if (Value.Type is SqlCharacterType) {
				builder.Append("'");
			}
				
			(Value as ISqlFormattable).AppendTo(builder);

			if (Value.Type is SqlCharacterType) {
				builder.Append("'");
			}
		}

		protected override void GetObjectData(SerializationInfo info) {
			info.SetValue("value", Value);
		}
	}
}