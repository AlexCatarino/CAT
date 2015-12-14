using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// This is the KeeapAlive command
    /// </summary>
    [DataContract]
    public class KeepAlive : Command
    {
        /// <summary>
        /// Class name (action)
        /// </summary>
        public const string CLASSNAME = "KeepAlive";

        public KeepAlive()
            : base(CLASSNAME, 1)
        {
                
        }

        public new string Serialize()
        {
            return JSONSerializer.Serialize<KeepAlive>(this);
        }

        public new static KeepAlive Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<KeepAlive>(serialized);
        }

        public new string ToString()
        {
            return this.Serialize();
        }

    }
}
