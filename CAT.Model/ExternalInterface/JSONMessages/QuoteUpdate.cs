using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Collections;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// Response for QuoteRequest when it arrives an update in quote grid
    /// </summary>
    [DataContract]
    public class QuoteUpdate : Command
    {
        /// <summary>
        /// Class Name (action)
        /// </summary>
        public const string CLASSNAME = "QuoteUpdate";

        /// <summary>
        /// Quote
        /// </summary>
        [DataMember(IsRequired = true, EmitDefaultValue = true)]
        public Quote quote;

        public QuoteUpdate()
            : base(CLASSNAME, 1)
        {

        }

        public QuoteUpdate(Quote quote)
            : base(CLASSNAME, 1)
        {
            this.quote = quote;
        }

        public new string Serialize()
        {
            return JSONSerializer.Serialize<QuoteUpdate>(this);
        }

        public new static QuoteUpdate Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<QuoteUpdate>(serialized);
        }

        public new string ToString()
        {
            return this.Serialize();
        }


    }
}
