using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// This message is used to notify disconnection from each side
    /// </summary>
    [DataContract]
    public class Disconnect : Command
    {
        /// <summary>
        /// Class Name (action)
        /// </summary>
        public const string CLASSNAME = "Disconnect";

        public Disconnect()
            : base(CLASSNAME, 1)
        {
                
        }
        
        public new string Serialize()
        {
            return JSONSerializer.Serialize<Disconnect>(this);
        }

        public new static Disconnect Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<Disconnect>(serialized);
        }

        public override string ToString()
        {
            return this.Serialize();
        }        
    }
}
