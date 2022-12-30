using EFCore.Sharding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEF.Common.Primitives
{
    public class DatabaseOptions
    {
        string _connectionString;
        public string ConnectionString
        {
            get
            {
                if (string.IsNullOrEmpty(this.SecurityKey))
                    return this._connectionString;
                else
                {
                    var securityKey = EncryptionDbConnection.DecodeSecurityKey(this.SecurityKey);
                    var connectionString = EncryptionDbConnection.Decode(this._connectionString, securityKey);
                    return connectionString;
                }
            }
            set
            {
                _connectionString = value;
            }
        }
        public ReadWriteType ReadWriteType { set; get; }
        public DatabaseType DatabaseType { get; set; }
        public string SecurityKey { set; get; }
    }
}
