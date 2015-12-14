using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Web;
using System.IO;

namespace CAT.Model
{
    /// <summary>
    /// This class helps JSON Serialization and Deserialization procedures
    /// </summary>
    class JSONSerializer
    {
        /// <summary>
        /// Serializes an object in JSON format
        /// </summary>
        /// <typeparam name="T">The type of the object</typeparam>
        /// <param name="obj">The object</param>
        /// <returns>a JSON serialized String</returns>
        public static string Serialize<T>(T obj)
        {
            System.Runtime.Serialization.Json.DataContractJsonSerializer serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(obj.GetType());
            MemoryStream ms = new MemoryStream();
            serializer.WriteObject(ms, obj);
            string retVal = Encoding.Default.GetString(ms.ToArray());
            ms.Dispose();
            return retVal;
        }

        /// <summary>
        /// Deserializes an object in JSON format
        /// </summary>
        /// <typeparam name="T">The type of the object</typeparam>
        /// <param name="json">The serialized JSON string</param>
        /// <returns>A deserialized object</returns>
        public static T Deserialize<T>(string json)
        {
            
            T obj = Activator.CreateInstance<T>();
            MemoryStream ms = new MemoryStream(Encoding.Unicode.GetBytes(json));
            
            System.Runtime.Serialization.Json.DataContractJsonSerializer serializer = new System.Runtime.Serialization.Json.DataContractJsonSerializer(obj.GetType());
            obj = (T)serializer.ReadObject(ms);
            
            ms.Close();
            ms.Dispose();
            return obj;            
        }

    }
}