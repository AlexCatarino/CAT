using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// This class is the response for PositionRequest.
    /// </summary>
    [DataContract]
    public class PositionResponse : Command
    {
        /// <summary>
        /// Class name (action)
        /// </summary>
        public const string CLASSNAME = "PositionResponse";


        /// <summary>
        ///  Array of user's positions
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public List<PositionItem> positions;
         
        public new string Serialize()
        {
            return JSONSerializer.Serialize<PositionResponse>(this);
        }

        public new static PositionResponse Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<PositionResponse>(serialized);
        }

        public override string ToString()
        {
            return this.Serialize();
        }
    }
}
