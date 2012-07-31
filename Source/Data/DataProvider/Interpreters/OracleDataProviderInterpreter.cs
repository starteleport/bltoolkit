﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using BLToolkit.Data.Sql.SqlProvider;
using BLToolkit.DataAccess;
using BLToolkit.Mapping;

namespace BLToolkit.Data.DataProvider.Interpreters
{
    public class OracleDataProviderInterpreter : DataProviderInterpreterBase
    {
        public override void SetParameterValue(IDbDataParameter parameter, object value)
        {
            if (value is TimeSpan)
            {
                parameter.Value = ((TimeSpan)value).ToString();
            }
            else
                base.SetParameterValue(parameter, value);
        }

        public override List<string> GetInsertBatchSqlList<T>(
            string insertText,
            IEnumerable<T> collection,
            MemberMapper[] members,
            int maxBatchSize)
        {
            //return GetInsertBatchSqlListWithInsertAll(insertText, collection, members, maxBatchSize);
            return GetInsertBatchSqlListUnionAll(insertText, collection, members, maxBatchSize);
        }

        private List<string> GetInsertBatchSqlListUnionAll<T>(
            string insertText,
            IEnumerable<T> collection,
            MemberMapper[] members,
            int maxBatchSize)
        {
            var sp = new OracleSqlProvider();
            var n = 0;
            var sqlList = new List<string>();

            var indexValuesWord = insertText.IndexOf(" VALUES (", StringComparison.Ordinal);
            var initQuery = insertText.Substring(0, indexValuesWord) + Environment.NewLine;
            var valuesQuery = insertText.Substring(indexValuesWord + 9);
            var indexEndValuesQuery = valuesQuery.IndexOf(")");
            valuesQuery = valuesQuery.Substring(0, indexEndValuesQuery)
                            .Replace("\r", "")
                            .Replace("\n", "")
                            .Replace("\t", "");

            var valuesWihtoutSequence = valuesQuery.Substring(valuesQuery.IndexOf(",") + 1);

            var sb = new StringBuilder(initQuery);
            sb.Append(" SELECT ");
            sb.AppendFormat(valuesQuery, members.Select(m => m.Name).ToArray());
            sb.AppendLine(" FROM (");

            initQuery = sb.ToString();

            sb = new StringBuilder(initQuery);
            bool isFirstValues = true;

            foreach (var item in collection)
            {
                if (!isFirstValues)
                    sb.AppendLine(" UNION ALL ");

                sb.Append("SELECT ");

                var values = new List<object>();
                foreach (var member in members)
                {
                    var sbItem = new StringBuilder();

                    var value = member.GetValue(item);

                    if (value is DateTime?)
                        value = ((DateTime?)value).Value;

                    sp.BuildValue(sbItem, value);

                    values.Add(sbItem + " " + member.Name);
                }

                sb.AppendFormat(valuesWihtoutSequence, values.ToArray());
                sb.Append(" FROM DUAL");

                isFirstValues = false;

                n++;
                if (n > maxBatchSize)
                {
                    sb.Append(")");
                    sqlList.Add(sb.ToString());
                    sb = new StringBuilder(initQuery);
                    isFirstValues = true;
                    n = 0;
                }
            }

            if (n > 0)
            {
                sb.Append(")");
                sqlList.Add(sb.ToString());
            }
            return sqlList;
        }

        private List<string> GetInsertBatchSqlListWithInsertAll<T>(
            string insertText,
            IEnumerable<T> collection,
            MemberMapper[] members,
            int maxBatchSize)
        {
            var sb = new StringBuilder();
            var sp = new OracleSqlProvider();
            var n = 0;
            var sqlList = new List<string>();

            foreach (var item in collection)
            {
                if (sb.Length == 0)
                    sb.AppendLine("INSERT ALL");

                string strItem = "\t" + insertText
                                            .Replace("INSERT INTO", "INTO")
                                            .Replace("\r", "")
                                            .Replace("\n", "")
                                            .Replace("\t", " ")
                                            .Replace("( ", "(");

                var values = new List<object>();
                foreach (var member in members)
                {
                    var sbItem = new StringBuilder();

                    var keyGenerator = member.MapMemberInfo.KeyGenerator as SequenceKeyGenerator;
                    if (keyGenerator != null)
                    {
                        values.Add(NextSequenceQuery(keyGenerator.Sequence));
                    }
                    else
                    {
                        var value = member.GetValue(item);

                        if (value is DateTime?)
                            value = ((DateTime?)value).Value;

                        sp.BuildValue(sbItem, value);

                        values.Add(sbItem.ToString());
                    }
                }

                sb.AppendFormat(strItem, values.ToArray());
                sb.AppendLine();

                n++;

                if (n >= maxBatchSize)
                {
                    sb.AppendLine("SELECT * FROM dual");

                    var sql = sb.ToString();
                    sqlList.Add(sql);

                    n = 0;
                    sb.Length = 0;
                }
            }

            if (n > 0)
            {
                sb.AppendLine("SELECT * FROM dual");

                var sql = sb.ToString();
                sqlList.Add(sql);
            }

            return sqlList;
        }

        public override string GetSequenceQuery(string sequenceName)
        {
            return string.Format("SELECT {0}.NEXTVAL FROM DUAL", sequenceName);
        }

        public override string NextSequenceQuery(string sequenceName)
        {
            return string.Format("{0}.NEXTVAL", sequenceName);
        }

        public override string GetReturningInto(string columnName)
        {
            return string.Format("returning {0} into :IDENTITY_PARAMETER", columnName);
        }
    }
}