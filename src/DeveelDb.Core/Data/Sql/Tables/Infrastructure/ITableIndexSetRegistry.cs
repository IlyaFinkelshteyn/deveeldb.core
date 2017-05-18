﻿using Deveel.Data.Indexes;

namespace Deveel.Data.Sql.Tables.Infrastructure {
	public interface ITableIndexSetRegistry {
		void SetTableIndexSet(ObjectName tableName, IIndexSet<SqlObject, long> indexSet);

		IIndexSet<SqlObject, long> GetTableIndexSet(ObjectName tableName);
	}
}