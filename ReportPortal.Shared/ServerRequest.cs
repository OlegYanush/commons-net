using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ReportPortal.Shared
{
    [DataContract]
    public class ServerRequest
    {
        public Guid TaskId { get; set; }

        [DataMember]
        public ServerAction Action { get; set; }

        [DataMember]
        public Guid ParentTestTaskId { get; set; }

        [DataMember]
        public Guid ParentLaunchTaskId { get; set; }

        public string ReportPortalItemId { get; set; }

        [DataMember]
        public string Body { get; set; }

        [DataMember]
        public Dictionary<string, string> AdditionalInfo { get; set; }
    }

    public enum ServerAction
    {
        StartLaunch,
        FinishLaunch,
        UpdateLaunch,
        StartTest,
        FinishTest,
        UpdateTest,
        AddLog,
        WaitReporting,
        Exit
    }
}
