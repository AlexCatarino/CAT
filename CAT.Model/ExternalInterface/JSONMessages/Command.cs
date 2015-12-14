using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace CAT.Model.ExternalInterface.JSONMessages
{
    /// <summary>
    /// This is the base class for all commands
    /// </summary>
    [DataContract]
    public class Command
    {
        /// <summary>
        /// Action is the name of the command
        /// </summary>
        [DataMember]
        public string action;

        /// <summary>
        /// Version of the command
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public int version;

        /// <summary>
        /// Successful Command flag
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = true)]
        public bool success;

        /// <summary>
        /// Reason of an unsuccessful command
        /// </summary>
        [DataMember(IsRequired = false, EmitDefaultValue = false)]
        public string reason;

        public Command(
             string action,
             int version)
        {
            this.action = action;
            this.version = version;
            this.success = true;
        }

        public Command(
             string action,
             int version,
            bool success)
        {
            this.action = action;
            this.version = version;
            this.success = success;
        }

        public Command()
        {
            
        }        
        
        /// <summary>
        /// Serializes this command
        /// </summary>
        /// <returns></returns>
        public string Serialize()
        {
            return JSONSerializer.Serialize<Command>(this);
        }

        /// <summary>
        /// Deserializes a command
        /// </summary>
        /// <param name="serialized">serialized message</param>
        /// <returns></returns>
        public static Command Deserialize(string serialized)
        {
            return JSONSerializer.Deserialize<Command>(serialized);
        }
    }
}
