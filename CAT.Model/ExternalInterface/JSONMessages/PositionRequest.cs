using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// This class creates a Position Request. 
    /// This message will be replied by Quantsis Connection Box with a PositionResponse message.
    /// </summary>
    [DataContract]
    public class PositionRequest : Command
    {
        /// <summary>
        /// Class name (action)
        /// </summary>
        public const string CLASSNAME = "PositionRequest";

        public PositionRequest()
            : base(CLASSNAME, 1)
        {
                
        }

        public new string Serialize()
        {
            return JSONSerializer.Serialize<PositionRequest>(this);
        }

        public new static PositionRequest Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<PositionRequest>(serialized);
        }

        public override string ToString()
        {
            return this.Serialize();
        }
    }
}
