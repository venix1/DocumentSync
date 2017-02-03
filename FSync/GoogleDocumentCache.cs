namespace FSync
{
	using System;
	using System.ComponentModel;
	using System.Data;
	using System.Data.Linq;
	using System.Data.Linq.Mapping;
	using System.Diagnostics;


	public partial class GoogleDocumentCache
	{
		static readonly string INDEX_CREATE_TABLE = @"CREATE TABLE IF NOT EXISTS GoogleIndex ( -- ID Path mapping
        	Id      TEXT PRIMARY KEY, -- Google Document Id
            Parent  TEXT,             -- Google Document Id of Parent
            Name    TEXT,             -- Name of Document
            Version INTEGER           -- Current version
        );";

		partial void OnCreated()
		{
			// if (!DatabaseExists())
			CreateDatabase();
		}

		new public void CreateDatabase()
		{
			ExecuteCommand(INDEX_CREATE_TABLE);
		}
	}

	public partial class GoogleDocumentIndex
	{
	}
}