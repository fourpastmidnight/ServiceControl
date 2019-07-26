namespace ServiceControl.Recoverability
{
    using System.Linq;

    public class ExceptionTypeAndStackTraceFailureClassifier : IFailureClassifier
    {
        public string Name => Id;

        public string ClassifyFailure(ClassifiableMessageDetails failure)
        {
            var exception = failure.Details?.Exception;

            if (exception == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(exception.StackTrace))
            {
                return GetNonStandardClassification(exception.ExceptionType);
            }

            
            var exceptionStackTrace = exception.StackTrace;
            if (exception.Message != null)
            {
                //The StackTrace message header contains the result of ToString call on the exception object so it includes the message
                //We need to remove the message in order to make sure the stack trace parser does not get into catastrophic backtracking mode.
                exceptionStackTrace = exceptionStackTrace.Replace(exception.Message, "");
            }
            var firstStackTraceFrame = StackTraceParser.Parse(exceptionStackTrace, (frame, type, method, parameterList, parameters, file, line) => new StackFrame
            {
                Type = type,
                Method = method,
                Params = parameterList,
                File = file,
                Line = line
            }).FirstOrDefault();

            if (firstStackTraceFrame != null)
            {
                return exception.ExceptionType + ": " + firstStackTraceFrame.ToMethodIdentifier();
            }

            return GetNonStandardClassification(exception.ExceptionType);
        }

        static string GetNonStandardClassification(string exceptionType)
        {
            return exceptionType + ": 0";
        }

        public const string Id = "Exception Type and Stack Trace";

        public class StackFrame
        {
            public string Type { get; set; }
            public string Method { get; set; }
            public string Params { get; set; }
            public string File { get; set; }
            public string Line { get; set; }

            public string ToMethodIdentifier()
            {
                return Type + "." + Method + Params;
            }
        }
    }
}