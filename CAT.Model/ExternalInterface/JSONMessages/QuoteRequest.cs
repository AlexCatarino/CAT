using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// This message is used to subscribe to quotes. 
    /// A QuoteResponse will be retrieved with the current quote grid line.
    /// QuoteUpdate messages will be retrieved with the updated fields.
    /// </summary>
    [DataContract]
    public class QuoteRequest : Command
    {
        /// <summary>
        /// Class Name (action)
        /// </summary>
        public const string CLASSNAME = "QuoteRequest";

        /// <summary>
        /// Security List to be subscribed
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public List<Security> securities;


        public QuoteRequest(List<Security> securities)
            : base(CLASSNAME, 1)
        {
            this.securities = securities;
        }

        public new string Serialize()
        {
            return JSONSerializer.Serialize<QuoteRequest>(this);
        }

        public new static QuoteRequest Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<QuoteRequest>(serialized);
        }

        public new string ToString()
        {
            return this.Serialize();
        }        

    }
}
