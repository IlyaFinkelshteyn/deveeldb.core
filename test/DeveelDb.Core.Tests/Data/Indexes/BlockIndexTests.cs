﻿using System;
using System.Collections.Generic;

using Deveel.Data.Sql;

using Xunit;

namespace Deveel.Data.Indexes {
	public static class BlockIndexTests {
		[Fact]
		public static void CreateBlockIndex() {
			var index = new BlockIndex<SqlObject, long>();
			index.Add(67);
			index.Add(90);

			Assert.Equal(2, index.Count);
		}

		[Fact]
		public static void InsertSort() {
			var index = new BlockIndex<SqlObject, long>();
			var items = new BigList<SqlObject>();
			items.Add(SqlObject.Integer(435));
			items.Add(SqlObject.Integer(1920));

			var comparer = new IndexComparer(items);

			index.InsertSort(SqlObject.Double(2234.99), 45, comparer);
			
			Assert.Equal(1, index.Count);
		}

		[Fact]
		public static void InsertSort_NoCompare() {
			var index = new BlockIndex<SqlObject, long>();
			index.InsertSort(6);

			Assert.Equal(1, index.Count);
		}

		[Fact]
		public static void SearchFirst_Sort() {
			var index = new BlockIndex<SqlObject, long>();
			var items = new BigList<SqlObject>();
			items.Add(SqlObject.Integer(435));
			items.Add(SqlObject.Integer(1920));
			items.Add(SqlObject.Integer(9182));

			var comparer = new IndexComparer(items);

			index.InsertSort(items[0], 0, comparer);
			index.InsertSort(items[1], 1, comparer);
			index.InsertSort(items[2], 2, comparer);

			Assert.Equal(3, index.Count);

			var first = index.SearchFirst(SqlObject.Integer(435), comparer);
			Assert.Equal(0, first);
		}


		[Fact]
		public static void SearcLast_Sort() {
			var index = new BlockIndex<SqlObject, long>();
			var items = new BigList<SqlObject>();
			items.Add(SqlObject.Integer(435));
			items.Add(SqlObject.Integer(1920));
			items.Add(SqlObject.Integer(9182));

			var comparer = new IndexComparer(items);

			index.InsertSort(items[0], 0, comparer);
			index.InsertSort(items[1], 1, comparer);
			index.InsertSort(items[2], 2, comparer);

			Assert.Equal(3, index.Count);

			var first = index.SearchLast(SqlObject.Integer(435), comparer);
			Assert.Equal(0, first);
		}

		[Fact]
		public static void AddAndRemoveItem() {
			var index = new BlockIndex<SqlObject, long>();
			var items = new BigList<SqlObject>();
			items.Add(SqlObject.Integer(435));
			items.Add(SqlObject.Integer(1920));
			items.Add(SqlObject.Integer(9182));

			var comparer = new IndexComparer(items);

			index.InsertSort(items[0], 0, comparer);
			index.InsertSort(items[1], 1, comparer);
			index.InsertSort(items[2], 2, comparer);

			Assert.Equal(3, index.Count);

			var value = index.RemoveSort(SqlObject.Integer(435), 0, comparer);

			Assert.Equal(0, value);
		}

		[Fact]
		public static void AddAndRemoveItemAt_NoSort() {
			var index = new BlockIndex<SqlObject, long>();
			index.Add(566);
			index.Add(54);
			index.Add(902);

			Assert.Equal(3, index.Count);
			Assert.Equal(566, index[0]);
			Assert.Equal(54, index[1]);
			Assert.Equal(902, index[2]);

			Assert.Equal(566, index.RemoveAt(0));
		}

		[Fact]
		public static void AddAndRemoveItem_NaturalSort() {
			var index = new BlockIndex<SqlObject, long>();
			index.InsertSort(566);
			index.InsertSort(54);
			index.InsertSort(902);

			Assert.True(index.RemoveSort(902));
			Assert.False(index.RemoveSort(102));
			Assert.False(index.RemoveSort(902));
		}

		[Fact]
		public static void Contains_NoSort() {
			var index = new BlockIndex<SqlObject, long>();
			var items = new BigList<SqlObject>();
			items.Add(SqlObject.Integer(435));
			items.Add(SqlObject.Integer(1920));
			items.Add(SqlObject.Integer(9182));

			var comparer = new IndexComparer(items);

			index.InsertSort(items[0], 0, comparer);
			index.InsertSort(items[1], 1, comparer);
			index.InsertSort(items[2], 2, comparer);

			Assert.Equal(3, index.Count);

			Assert.False(index.Contains(5));
			Assert.True(index.Contains(2));
		}

		[Fact]
		public static void CreateFromOtherIndex() {
			var index = new BlockIndex<SqlObject, long>();
			var items = new BigList<SqlObject>();
			items.Add(SqlObject.Integer(435));
			items.Add(SqlObject.Integer(1920));
			items.Add(SqlObject.Integer(9182));

			var comparer = new IndexComparer(items);

			index.InsertSort(items[0], 0, comparer);
			index.InsertSort(items[1], 1, comparer);
			index.InsertSort(items[2], 2, comparer);

			var index2 = new BlockIndex<SqlObject, long>(index);

			Assert.Equal(index.Count, index2.Count);
		}

		[Fact]
		public static void FillBlocks() {
			var index = new BlockIndex<SqlObject, long>();
			FillIndexWith(index, 1024);

			Assert.Equal(1024, index.Count);
		}

		[Fact]
		public static void RemoveAllBlocks() {
			var index = new BlockIndex<SqlObject, long>();
			FillIndexWith(index, 2000);

			Assert.Equal(2000, index.Count);

			RemoveAll(index, 2000);

			Assert.Equal(0, index.Count);
		}

		[Fact]
		public static void ContainsItem() {
			var index = new BlockIndex<SqlObject, long>();
			var items = new BigList<SqlObject>();
			items.Add(SqlObject.Integer(435));
			items.Add(SqlObject.Integer(1920));
			items.Add(SqlObject.Integer(9182));

			var comparer = new IndexComparer(items);

			index.InsertSort(items[0], 0, comparer);
			index.InsertSort(items[1], 1, comparer);
			index.InsertSort(items[2], 2, comparer);

			Assert.True(index.Contains(SqlObject.Integer(435), comparer));
			Assert.False(index.Contains(SqlObject.Integer(21), comparer));
		}

		[Fact]
		public static void InsertInNonEmptyIndex() {
			var index = new BlockIndex<SqlObject, long>();
			FillIndexWith(index, 300);

			Assert.Equal(300, index.Count);
			index.Insert(29, 45);

			Assert.Equal(301, index.Count);
			Assert.Equal(45, index[29]);
		}

		[Fact]
		public static void RemoveFromEnumerator() {
			var index = new BlockIndex<SqlObject, long>();
			FillIndexWith(index, 1024);

			var enumerator = index.GetEnumerator();
			Assert.True(enumerator.MoveNext());
			enumerator.Remove();
			Assert.True(enumerator.MoveNext());
			enumerator.Remove();

			while (enumerator.MoveNext()) {
				enumerator.Remove();
			}

			Assert.False(enumerator.MoveNext());
			Assert.Equal(0, index.Count);
		}

		private static void FillIndexWith(BlockIndex<SqlObject, long> index, int count) {
			var items = new BigList<SqlObject>();

			for (int i = 0; i < count; i++) {
				items.Add(SqlObject.Integer(i*2));
			}

			var comparer = new IndexComparer(items);

			for (int i = 0; i < items.Count; i++) {
				index.InsertSort(items[i], i, comparer);
			}
		}

		private static void RemoveAll(BlockIndex<SqlObject, long> index, int count) {
			var items = new BigList<SqlObject>();

			for (int i = 0; i < count; i++) {
				items.Add(SqlObject.Integer(i * 2));
			}

			var comparer = new IndexComparer(items);
			for (int i = 0; i < items.Count; i++) {
				index.RemoveSort(items[i], i, comparer);
			}
		}

		#region IndexComparer

		class IndexComparer : IIndexComparer<SqlObject, long> {
			private readonly BigList<SqlObject> items;

			public IndexComparer(BigList<SqlObject> items) {
				this.items = items;
			}

			public int Compare(long indexed, SqlObject key) {
				var value = items[indexed];
				return value.CompareTo(key);
			}
		}

		#endregion
	}
}