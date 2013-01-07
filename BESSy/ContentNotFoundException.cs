/*
Copyright © 2011, Kristen Mallory DBA klink.
All rights reserved.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BESSy
{
    public class ContentNotFoundException : System.SystemException
    {
        private string message { get; set; }

        public ContentNotFoundException(string entityName, int id)
        {
            message = string.Format("Entity {0} not found for Id '{1}'", entityName, id);
        }

        public ContentNotFoundException(string entityName, string key)
        {
            message = string.Format("Entity {0} not found for Key '{1}'", entityName, key);
        }

        public override string Message
        {
            get
            {
                return message;
            }
        }
    }
}
