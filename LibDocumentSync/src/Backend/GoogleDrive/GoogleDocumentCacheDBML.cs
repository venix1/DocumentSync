// 
//  ____  _     __  __      _        _ 
// |  _ \| |__ |  \/  | ___| |_ __ _| |
// | | | | '_ \| |\/| |/ _ \ __/ _` | |
// | |_| | |_) | |  | |  __/ || (_| | |
// |____/|_.__/|_|  |_|\___|\__\__,_|_|
//
// Auto-generated from main on 2017-02-03 16:33:32Z.
// Please visit http://code.google.com/p/dblinq2007/ for more information.
//
namespace DocumentSync
{
    using System;
    using System.ComponentModel;
    using System.Data;
    using System.Data.Linq;
    using System.Data.Linq.Mapping;
    using System.Diagnostics;
    
    
    public partial class GoogleDocumentCache : DataContext
    {
        
        #region Extensibility Method Declarations
        partial void OnCreated();
        #endregion
        
        
        public GoogleDocumentCache(string connectionString) : 
                base(connectionString)
        {
            this.OnCreated();
        }
        
        public GoogleDocumentCache(IDbConnection connection) : 
                base(connection)
        {
            this.OnCreated();
        }
        
        public GoogleDocumentCache(string connection, MappingSource mappingSource) : 
                base(connection, mappingSource)
        {
            this.OnCreated();
        }
        
        public GoogleDocumentCache(IDbConnection connection, MappingSource mappingSource) : 
                base(connection, mappingSource)
        {
            this.OnCreated();
        }
        
        public Table<GoogleDocumentIndex> DocumentIndex
        {
            get
            {
                return this.GetTable <GoogleDocumentIndex>();
            }
        }
    }
    
    [Table(Name="GoogleDocumentIndex")]
    public partial class GoogleDocumentIndex : System.ComponentModel.INotifyPropertyChanging, System.ComponentModel.INotifyPropertyChanged
    {
        
        private static System.ComponentModel.PropertyChangingEventArgs emptyChangingEventArgs = new System.ComponentModel.PropertyChangingEventArgs("");
        
        private string _Id;
        
        private string _Parent;
        
        private string _Name;
        
        private long _Version;
        
        #region Extensibility Method Declarations
        partial void OnCreated();
        
        partial void OnIdChanged();
        
        partial void OnIdChanging(string value);
        
        partial void OnParentChanged();
        
        partial void OnParentChanging(string value);
        
        partial void OnNameChanged();
        
        partial void OnNameChanging(string value);
        
        partial void OnVersionChanged();
        
        partial void OnVersionChanging(long value);
        #endregion
        
        
        public GoogleDocumentIndex()
        {
            this.OnCreated();
        }
        
        [Column(Storage="_Id", Name=null, DbType=null, IsPrimaryKey=true, AutoSync=AutoSync.Never, CanBeNull=false)]
        [DebuggerNonUserCode()]
        public string Id
        {
            get
            {
                return this._Id;
            }
            set
            {
                if (((_Id == value) 
                            == false))
                {
                    this.OnIdChanging(value);
                    this.SendPropertyChanging();
                    this._Id = value;
                    this.SendPropertyChanged("Id");
                    this.OnIdChanged();
                }
            }
        }
        
        [Column(Storage="_Parent", Name=null, DbType=null, AutoSync=AutoSync.Never)]
        [DebuggerNonUserCode()]
        public string Parent
        {
            get
            {
                return this._Parent;
            }
            set
            {
                if (((_Parent == value) 
                            == false))
                {
                    this.OnParentChanging(value);
                    this.SendPropertyChanging();
                    this._Parent = value;
                    this.SendPropertyChanged("Parent");
                    this.OnParentChanged();
                }
            }
        }
        
        [Column(Storage="_Name", Name=null, DbType=null, AutoSync=AutoSync.Never, CanBeNull=false)]
        [DebuggerNonUserCode()]
        public string Name
        {
            get
            {
                return this._Name;
            }
            set
            {
                if (((_Name == value) 
                            == false))
                {
                    this.OnNameChanging(value);
                    this.SendPropertyChanging();
                    this._Name = value;
                    this.SendPropertyChanged("Name");
                    this.OnNameChanged();
                }
            }
        }
        
        [Column(Storage="_Version", Name=null, DbType=null, AutoSync=AutoSync.Never, CanBeNull=false)]
        [DebuggerNonUserCode()]
        public long Version
        {
            get
            {
                return this._Version;
            }
            set
            {
                if ((_Version != value))
                {
                    this.OnVersionChanging(value);
                    this.SendPropertyChanging();
                    this._Version = value;
                    this.SendPropertyChanged("Version");
                    this.OnVersionChanged();
                }
            }
        }
        
        public event System.ComponentModel.PropertyChangingEventHandler PropertyChanging;
        
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void SendPropertyChanging()
        {
            System.ComponentModel.PropertyChangingEventHandler h = this.PropertyChanging;
            if ((h != null))
            {
                h(this, emptyChangingEventArgs);
            }
        }
        
        protected virtual void SendPropertyChanged(string propertyName)
        {
            System.ComponentModel.PropertyChangedEventHandler h = this.PropertyChanged;
            if ((h != null))
            {
                h(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
