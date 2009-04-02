﻿using System;
using System.Text;

namespace BLToolkit.Data.Sql.SqlProvider
{
	using DataProvider;

	public class BasicSqlProvider : ISqlProvider
	{
		#region Init

		public BasicSqlProvider(DataProviderBase dataProvider)
		{
			_dataProvider = dataProvider;
		}

		readonly DataProviderBase _dataProvider;

		SqlBuilder    _sqlBuilder;
		StringBuilder _sb;
		int           _indent;

		#endregion

		#region BuildSQL

		public string BuildSql(SqlBuilder sqlBuilder)
		{
			StringBuilder sb = new StringBuilder();

			BuildSql(sqlBuilder, sb, 0);

			return sb.ToString();
		}

		void BuildSql(SqlBuilder sqlBuilder, StringBuilder sb, int indent)
		{
			_sqlBuilder = sqlBuilder;
			_sb         = sb;
			_indent     = indent;

			BuildSelectClause();
			BuildFromClause  ();
		}

		#endregion

		#region Overrides

		protected virtual void BuildSqlBuilder(SqlBuilder sqlBuilder, StringBuilder sb, int indent)
		{
			new BasicSqlProvider(_dataProvider).BuildSql(sqlBuilder, sb, indent);
		}

		#endregion

		#region Build Select

		void BuildSelectClause()
		{
			AppendIndent();

			_sb.Append("SELECT").AppendLine();

			_indent++;

			foreach (SqlBuilder.Column col in _sqlBuilder.Select.Columns)
			{
				AppendIndent();
				BuildColumn(col);

				if (!string.IsNullOrEmpty(col.Alias))
					_sb.Append(" ").Append(col.Alias);

				_sb.Append(',').AppendLine();
			}

			_indent--;

			_sb
				.Remove(_sb.Length - Environment.NewLine.Length - 1, Environment.NewLine.Length + 1)
				.AppendLine();
		}

		void BuildColumn(SqlBuilder.Column col)
		{
			if (col.Expression is SqlField)
			{
				SqlField field = (SqlField)col.Expression;

				string table = GetTableAlias(_sqlBuilder.From[field.Table]) ?? GetTablePhysicalName(field.Table);

				if (string.IsNullOrEmpty(table))
					throw new SqlException(string.Format("Table {0} should have alias.", field.Table));

				_sb.Append(table).Append('.');

				if   (field.Name == "*") _sb.Append(field.PhysicalName);
				else _sb.Append(_dataProvider.Convert(field.PhysicalName, ConvertType.NameToQueryField));
			}
			else if (col.Expression is SqlBuilder.Column)
			{
				SqlBuilder.Column column = (SqlBuilder.Column)col.Expression;

				string table = GetTableAlias(_sqlBuilder.From[column.Parent]) ?? GetTablePhysicalName(column.Parent);

				if (string.IsNullOrEmpty(table))
					throw new SqlException(string.Format("Table {0} should have alias.", column.Parent));

				_sb
					.Append(table)
					.Append('.')
					.Append(_dataProvider.Convert(column.Alias, ConvertType.NameToQueryField));
			}
			else
				throw new InvalidOperationException();
		}

		#endregion

		#region Build From

		void BuildFromClause()
		{
			AppendIndent();

			_sb.Append("FROM").AppendLine();

			_indent++;

			foreach (SqlBuilder.TableSource ts in _sqlBuilder.From.Tables)
			{
				AppendIndent();

				BuildPhysicalTable(ts.Source);

				string alias = GetTableAlias(ts);

				if (!string.IsNullOrEmpty(alias))
					_sb.Append(" ").Append(alias);
			}

			_indent--;

			_sb.AppendLine();
		}

		void BuildPhysicalTable(ISqlTableSource table)
		{
			if (table is SqlTable || table is SqlBuilder.TableSource)
			{
				_sb.Append(GetTablePhysicalName(table));
			}
			else if (table is SqlBuilder)
			{
				_sb.Append("(").AppendLine();
				BuildSqlBuilder((SqlBuilder)table, _sb, _indent + 1);
				AppendIndent().Append(")");
			}
			else
				throw new InvalidOperationException();
		}

		#endregion

		#region Helpers

		static string GetTableAlias(ISqlTableSource table)
		{
			if (table is SqlBuilder.TableSource)
			{
				SqlBuilder.TableSource ts = (SqlBuilder.TableSource)table;
				return string.IsNullOrEmpty(ts.Alias) ? GetTableAlias(ts.Source) : ts.Alias;
			}

			if (table is SqlTable)
				return ((SqlTable)table).Alias;

			throw new InvalidOperationException();
		}

		string GetTablePhysicalName(ISqlTableSource table)
		{
			if (table is SqlTable)
				return _dataProvider.Convert(((SqlTable)table).PhysicalName, ConvertType.NameToQueryTable).ToString();

			if (table is SqlBuilder.TableSource)
				return GetTablePhysicalName(table);

			throw new InvalidOperationException();
		}

		StringBuilder AppendIndent()
		{
			if (_indent > 0)
				_sb.Append('\t', _indent);

			return _sb;
		}

		#endregion
	}
}
