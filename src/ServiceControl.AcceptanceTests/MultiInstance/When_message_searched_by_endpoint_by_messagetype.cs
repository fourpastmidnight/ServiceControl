﻿namespace ServiceBus.Management.AcceptanceTests.MultiInstance
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using EndpointTemplates;
    using Infrastructure.Settings;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.Settings;
    using NUnit.Framework;
    using ServiceControl.CompositeViews.Messages;
    using ServiceControl.Infrastructure.Settings;
    using Conventions = NServiceBus.AcceptanceTesting.Customization.Conventions;

    class When_message_searched_by_endpoint_by_messagetype : AcceptanceTest
    {
        [Test]
        public async Task Should_be_found()
        {
            SetInstanceSettings = ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues;

            var response = new List<MessagesView>();

            var endpointName = Conventions.EndpointNamingConvention(typeof(ReceiverRemote));

            //search for the message type
            var searchString = typeof(MyMessage).Name;

            var context = await Define<MyContext>(Remote1, Master)
                .WithEndpoint<Sender>(b => b.When((bus, c) => bus.Send(new MyMessage())))
                .WithEndpoint<ReceiverRemote>()
                .Done(async c =>
                {
                    var result = await this.TryGetMany<MessagesView>($"/api/endpoints/{endpointName}/messages/search/" + searchString, instanceName: Master);
                    response = result;
                    return result && response.Count == 1;
                })
                .Run();

            var expectedRemote1InstanceId = InstanceIdGenerator.FromApiUrl(SettingsPerInstance[Remote1].ApiUrl);

            var remote1Message = response.SingleOrDefault(msg => msg.MessageId == context.Remote1MessageId);

            Assert.NotNull(remote1Message, "Remote1 message not found");
            Assert.AreEqual(expectedRemote1InstanceId, remote1Message.InstanceId, "Remote1 instance id mismatch");
        }

        void ConfigureRemoteInstanceForMasterAsWellAsAuditAndErrorQueues(string instanceName, Settings settings)
        {
            switch (instanceName)
            {
                case Remote1:
                    addressOfRemote = settings.ApiUrl;
                    settings.AuditQueue = AuditRemote;
                    settings.ErrorQueue = ErrorRemote;
                    break;
                case Master:
                    settings.RemoteInstances = new[]
                    {
                        new RemoteInstanceSetting
                        {
                            ApiUri = addressOfRemote,
                            QueueAddress = Remote1
                        }
                    };
                    settings.AuditQueue = AuditMaster;
                    settings.ErrorQueue = ErrorMaster;
                    break;
            }
        }

        string addressOfRemote;
        const string Master = "master";
        const string Remote1 = "remote1";
        static string AuditMaster = $"{Master}.audit";
        static string ErrorMaster = $"{Master}.error";
        static string AuditRemote = $"{Remote1}.audit";
        static string ErrorRemote = $"{Remote1}.error";

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    c.AuditProcessedMessagesTo(AuditMaster);
                    c.SendFailedMessagesTo(ErrorMaster);
                    c.ConfigureTransport()
                        .Routing()
                        .RouteToEndpoint(typeof(MyMessage), typeof(ReceiverRemote));
                });
            }
        }

        public class ReceiverRemote : EndpointConfigurationBuilder
        {
            public ReceiverRemote()
            {
                EndpointSetup<DefaultServerWithAudit>(c =>
                {
                    c.AuditProcessedMessagesTo(AuditRemote);
                    c.SendFailedMessagesTo(ErrorRemote);
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public MyContext Context { get; set; }

                public ReadOnlySettings Settings { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    Context.EndpointNameOfReceivingEndpoint = Settings.EndpointName();
                    Context.Remote1MessageId = context.MessageId;
                    return Task.FromResult(0);
                }
            }
        }


        public class MyMessage : ICommand
        {
        }

        public class MyContext : ScenarioContext
        {
            public string Remote1MessageId { get; set; }

            public string EndpointNameOfReceivingEndpoint { get; set; }
        }
    }
}