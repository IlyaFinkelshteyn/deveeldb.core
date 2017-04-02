﻿using System;

using Xunit;

namespace Deveel.Data.Sql {
	public static class SqlYearToMonthTests {
		[Theory]
		[InlineData(3, 36)]
		[InlineData(1, 12)]
		public static void FromYear(int years, int expectedMonths) {
			var ytm = new SqlYearToMonth(years, 0);

			Assert.NotNull(ytm);
			Assert.False(ytm.IsNull);
			Assert.Equal(expectedMonths, ytm.TotalMonths);
			Assert.Equal(years, ytm.TotalYears);
		}

		[Theory]
		[InlineData(34, 2.8333333333333335)]
		[InlineData(24, 2)]
		public static void FromMonths(int months, double expectedYears) {
			var ytm = new SqlYearToMonth(months);

			Assert.NotNull(ytm);
			Assert.False(ytm.IsNull);
			Assert.Equal(expectedYears, ytm.TotalYears);
			Assert.Equal(months, ytm.TotalMonths);;
		}

		[Theory]
		[InlineData(1, 12, 24)]
		[InlineData(2, 0, 24)]
		public static void FromYearsAndMonths(int years, int months, int expectedMonths) {
			var ytm = new SqlYearToMonth(years, months);

			Assert.NotNull(ytm);
			Assert.False(ytm.IsNull);
			Assert.Equal(expectedMonths, ytm.TotalMonths);
		}

		[Theory]
		[InlineData(56, 56, true)]
		[InlineData(23, 22, false)]
		public static void EqualToOther(int months1, int months2, bool expected) {
			var ytm1 = new SqlYearToMonth(months1);
			var ytm2 = new SqlYearToMonth(months2);

			Assert.Equal(expected, ytm1.Equals(ytm2));
		}

		[Theory]
		[InlineData(65, 65, 0)]
		[InlineData(21, 65, -1)]
		[InlineData(11, 21, -1)]
		[InlineData(33, 10, 1)]
		public static void Compare_ToNumber(int months, int value, int expected) {
			var ytm = new SqlYearToMonth(months);
			var number = (SqlNumber) value;

			var expectedResult = (SqlNumber) expected;
			var result = ytm.CompareTo(number);
			Assert.Equal(expectedResult, result);
		}

		[Theory]
		[InlineData(32, 43, -1)]
		[InlineData(22, 1, 1)]
		[InlineData(3, 3, 0)]
		public static void Compare_ToYearToMonth(int months1, int months2, int expected) {
			var ytm1 = new SqlYearToMonth(months1);
			var ytm2 = new SqlYearToMonth(months2);

			var expectedResult = (SqlNumber) expected;
			var result = ytm1.CompareTo(ytm2);
			Assert.Equal(expectedResult, result);
		}

		[Theory]
		[InlineData(15, 32, -1)]
		public static void Compare_ToSqlValue_Number(int months, int value, int expected) {
			var ytm = new SqlYearToMonth(months);
			var number = (SqlNumber)value;

			var result = (ytm as IComparable<ISqlValue>).CompareTo(number);
			Assert.Equal(expected, result);
		}

		[Theory]
		[InlineData(1, 2, -1)]
		[InlineData(54, 33, 1)]
		public static void Compare_ToSqlValue_YearToMonth(int months1, int months2, int expected) {
			var ytm1 = new SqlYearToMonth(months1);
			var ytm2 = new SqlYearToMonth(months2);

			var result = (ytm1 as IComparable<ISqlValue>).CompareTo(ytm2);
			Assert.Equal(expected, result);
		}


		[Fact]
		public static void FromNegativeMonths() {
			Assert.Throws<ArgumentException>(() => new SqlYearToMonth(-2));
		}

		[Fact]
		public static void InvalidComparison() {
			var ytm = new SqlYearToMonth(2);
			Assert.Throws<NotSupportedException>(() => (ytm as IComparable<ISqlValue>).CompareTo(SqlBoolean.True));
		}
	}
}