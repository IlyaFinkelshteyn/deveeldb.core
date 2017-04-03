﻿using System;

namespace Deveel.Data.Sql {
	public sealed class SqlNullType : SqlType {
		public SqlNullType()
			: base("NULL", SqlTypeCode.Null) {
		}

		public override bool IsInstanceOf(ISqlValue value) {
			return value.IsNull;
		}

		public override bool CanCastTo(SqlType destType) {
			return true;
		}

		public override ISqlValue Cast(ISqlValue value, SqlType destType) {
			return SqlNull.Value;
		}

		public override bool IsIndexable => false;
	}
}