using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// This message is used to unsubscribe to quotes
    /// </summary>
    [DataContract]
    public class QuoteUnsubscribeRequest : Command
    {
        /// <summary>
        /// Class Name (action)
        /// </summary>
        public const string CLASSNAME = "QuoteUnsubscribeRequest";

        /// <summary>
        /// Security List to be unsubscribed
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public List<Security> securities;

        public QuoteUnsubscribeRequest()
            : base(CLASSNAME, 1)
        {
                
        }

        public QuoteUnsubscribeRequest(List<Security> securities)
            : base(CLASSNAME, 1)
        {
            this.securities = securities;
        }

        public new string Serialize()
        {
            return JSONSerializer.Serialize<QuoteUnsubscribeRequest>(this);
        }

        public new static QuoteUnsubscribeRequest Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<QuoteUnsubscribeRequest>(serialized);
        }

        public new string ToString()
        {
            return this.Serialize();
        }
    }
}
