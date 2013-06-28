using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace BESSy.Replication
{
    public class ReplicationException : ApplicationException
    {
        public ReplicationException(Guid transactionId) : base() { Id = transactionId; }
        public ReplicationException(Guid transactionId, string message) : base(message) { Id = transactionId; }
        public ReplicationException(Guid transactionId, string message, Exception innerExcetion) : base(message, innerExcetion) { Id = transactionId; }
        public ReplicationException(Guid transactionId, SerializationInfo info, StreamingContext context) : base(info, context) { Id = transactionId; }

        public Guid Id { get; set; }
        public DateTime TimeStamp { get; set; }
    }
}
