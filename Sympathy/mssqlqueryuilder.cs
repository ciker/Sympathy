using System;
using System.Collections.Generic;

namespace Sympathy
{
	public class MSSqlQueryBuilder : QueryBuilder
	{
		public MSSqlQueryBuilder (Table table = null, QueryType type = QueryType.Select) : base (table, type)
		{
		}
		
		protected override string selectQuery ()
		{
			string query = "SELECT {0} FROM [{1}]";
			
			List<string> cols = new List<string> ();
			foreach (Column column in Table) 
			{
				cols.Add (string.Format ("[{0}]", column.Name));
			}
			
			query = string.Format (query, string.Join (", ", cols), Table.Name);
			
			List<string> where = new List<string> ();
			Dictionary <string, KeyValuePair<Operators, object>> criteria = parseKeys (_criteria);
			
			foreach (KeyValuePair<string, KeyValuePair<Operators, object>> item in criteria)
			{
				if (Table[item.Key] != null) {
					string _operator = OperatorsString [item.Value.Key];
					object value = item.Value.Value;
					
					if (Table[item.Key] != null) {
						if (value != null && typeof (iModel).IsAssignableFrom (value.GetType ())) {
							Reflector reflector = new Reflector ((iModel)value);
							Table table = reflector.getTable ();
							
							value = table.PrimaryKey.getValue ((iModel)value);
						}
						
						// string key = Utils.genrateNameFromType (item.Key);
						string key = Table[item.Key].Name;
						
						if (item.Value.Value == null) {
							where.Add (string.Format ("[{0}] IS NULL", key));
						} else if (item.Value.Value.GetType ().Equals (typeof (int)) ||
							item.Value.Value.GetType ().Equals (typeof (long)))
							where.Add (string.Format ("[{0}] {1} {2}", key, _operator, value));
						else if (item.Value.Value.GetType ().IsArray &&
						         typeof (string).IsAssignableFrom (item.Value.Value.GetType ()) &&
						         item.Value.Key == Operators.In) {
							where.Add (string.Format ("[{0}] {1} ({2})", key, _operator, string.Join (",", value)));
						} else if (item.Value.Key == Operators.In) {
							where.Add (string.Format ("[{0}] {1} ({2})", key, _operator, value));
						} else if (item.Value.Key == Operators.Between) {
							where.Add (string.Format ("[{0}] {1} {2}", key, _operator, value));
						} else if (value.GetType ().Equals ( typeof (Boolean)) && Table[item.Key].DbType !=  System.Data.DbType.String) {
							where.Add (string.Format ("[{0}] {1} '{2}'", key, _operator, (bool)value ? 1 : 0));
						} else {
							where.Add (string.Format ("[{0}] {1} '{2}'", key, _operator, value));
						}
					}
				}
			}
			
			if (where.Count > 0) {
				query += string.Format (" WHERE {0}", string.Join (" AND ", where));
			}
			
			if (OrderBy != null) {
				List<string> ordering = new List<string> ();
				foreach (KeyValuePair<string, object> item in OrderBy) {
					ordering.Add (string.Format ("{0} {1}", Table[item.Key].Name, item.Value));
				}
				
				if (ordering.Count > 0)
					query += string.Format (" ORDER BY {0}", string.Join (", ", ordering));
			}
			
			Console.WriteLine (query);

			return query;
		}
		
		protected override string deleteQuery ()
		{
			string query = selectQuery ();
			
			query = string.Format ("DELETE {0}", query.Substring (query.IndexOf ("FROM")));
			return query;
		}
		
		protected override string insertQuery (System.Collections.Generic.IDictionary<string, object> values)
		{
			string query = 
@"
BEGIN TRAN
	UPDATE [{0}] WITH (SERIALIZABLE) SET {1} WHERE {2};
	IF @@ROWCOUNT = 0
	BEGIN
		INSERT INTO [{0}] ({3}) VALUES ({4}); SELECT @@IDENTITY
	END

	SELECT @@IDENTITY
COMMIT TRAN
";

			// string query = "INSERT INTO [{0}] ({1}) VALUES ({2}); SELECT CAST(Scope_Identity() AS INT);";
			
			List<string> cols = new List<string> ();
			List<string> vals = new List<string> ();
			
			List<string> update = new List<string>();
			List<string> where  = new List<string>();
			
			_criteria.each ( (item, value) => where.Add (string.Format ("{0}='{1}'", item, value)) );
			
			foreach (KeyValuePair<string, object> item in values) {
				if (item.Value != null && Table[item.Key] != null) {
					cols.Add (string.Format ("[{0}]", item.Key.ToLower ()));
					
					System.Data.DbType colType = Table[item.Key].DbType;
					
					if (colType == System.Data.DbType.String || colType == System.Data.DbType.DateTime) {
						string value = item.Value.ToString ();
						
						if (item.Value.GetType () == typeof (System.DateTime)) {
							value = ((DateTime)item.Value).ToString ("yyyy-MM-dd HH:mm:ss");
						}	
						vals.Add (string.Format ("'{0}'", value));
						update.Add (string.Format ("[{0}] = '{1}'", Utils.genrateNameFromType (item.Key), value));
					} else {
						vals.Add (string.Format ("{0}", item.Value));
						update.Add (string.Format ("[{0}] = {1}", Utils.genrateNameFromType (item.Key), item.Value));
					}
				}
			}
			
			query = string.Format (
				query, 
				Table.Name, 
				string.Join (", ", update), 
				string.Join (", ", where),
				string.Join (", ", cols),
				string.Join (", ", vals)
			);
			
			Console.WriteLine (query);
			return query;
			// return string.Format (query, Table.Name, string.Join (", ", cols), string.Join (", ", cols));
		}
		
		protected override string updateQuery ()
		{
			string query = "UPDATE [{0}] SET {1} WHERE {2}";
			
			List<string> update = new List<string>();
			List<string> where  = new List<string>();
			
			// _values.each ( (item, value) => update.Add (string.Format ("{0}='{1}'", item, value)) );
			_criteria.each ( (item, value) => where.Add (string.Format ("{0}='{1}'", item, value)) );
			
			foreach (KeyValuePair<string, object> item in _values) {
				if (item.Value != null && Table[item.Key] != null) {
					System.Data.DbType colType = Table[item.Key].DbType;
					
					if (colType == System.Data.DbType.String || colType == System.Data.DbType.DateTime)
						update.Add (string.Format ("[{0}] = '{1}'", Utils.genrateNameFromType (item.Key), item.Value.ToString ()));
					else
						update.Add (string.Format ("[{0}] = {1}", Utils.genrateNameFromType (item.Key), item.Value));
				}
			}
			
			query = string.Format (query, Table.Name, string.Join (", ", update), string.Join (", ", where));
			return query;
		}
	}
}

