using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// Response for QuoteUnsubscribeRequest
    /// </summary>
    [DataContract]
    public class QuoteUnsubscribeResponse : Command
    {
        /// <summary>
        /// Class Name (action)
        /// </summary>
        public const string CLASSNAME = "QuoteUnsubscribeResponse";

        /// <summary>
        /// Security List unsubscribed
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public List<Security> securities;

        public QuoteUnsubscribeResponse()
            : base(CLASSNAME, 1)
        {

        }

        public QuoteUnsubscribeResponse(List<Security> securities)
            : base(CLASSNAME, 1)
        {
            this.securities = securities;
        }

        public new string Serialize()
        {
            return JSONSerializer.Serialize<QuoteUnsubscribeResponse>(this);
        }

        public new static QuoteUnsubscribeResponse Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<QuoteUnsubscribeResponse>(serialized);
        }

        public new string ToString()
        {
            return this.Serialize();
        }


    }
}
