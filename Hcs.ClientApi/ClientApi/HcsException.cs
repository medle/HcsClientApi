using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi
{
    public class HcsException : Exception
    {
        public HcsMemoryMessageCapture MessageCapture { get; private set;}

        public HcsException(string message): base(message)
        { 
        }

        public HcsException(string message, Exception nestedException) : base(message, nestedException)
        { 
        }

        public HcsException(string message, HcsMemoryMessageCapture capture, Exception nestedException) 
            : base(message, nestedException)
        {
            MessageCapture = capture;
        }

        public static HcsException FindHcsException(Exception e)
        {
            var hcsException = e as HcsException;
            if (hcsException != null) return hcsException;

            var aggregate = e as AggregateException;
            if (aggregate != null) {
                return aggregate.InnerExceptions.OfType<HcsException>().FirstOrDefault();
            }

            return null;
        }
    }
}
