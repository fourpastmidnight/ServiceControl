﻿namespace ServiceControl.Recoverability
{
    using NServiceBus;

    public class RetryOperationPreparing : IEvent
    {
        public string RequestId { get; set; }
        public RetryType RetryType { get; set; }
        public int TotalNumberOfMessages { get; set; }
        public Progress Progress { get; set; }
        public bool IsFailed { get; set; }
    }
}