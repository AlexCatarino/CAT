using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// Response for QuoteRequest with quote grid line
    /// </summary>
    [DataContract]
    public class QuoteResponse : Command
    {
        /// <summary>
        /// Class Name (action)
        /// </summary>
        public const string CLASSNAME = "QuoteResponse";

        /// <summary>
        /// Quote
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public Quote quote;

        public QuoteResponse()
            : base(CLASSNAME, 1)
        {

        }

        public QuoteResponse(Quote quote)
            : base(CLASSNAME, 1)
        {
            this.quote = quote;
        }

        public new string Serialize()
        {
            return JSONSerializer.Serialize<QuoteResponse>(this);
        }

        public new static QuoteResponse Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<QuoteResponse>(serialized);
        }

        public new string ToString()
        {
            return this.Serialize();
        }


    }
}
