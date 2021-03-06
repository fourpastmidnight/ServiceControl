﻿namespace ServiceBus.Management.AcceptanceTests.ExternalIntegrations
{
    using System;
    using System.Threading.Tasks;
    using Infrastructure.Settings;
    using NServiceBus;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;
    using ServiceControl.Contracts;
    using ServiceControl.Contracts.Operations;

    [TestFixture]
    class When_a_message_has_custom_checks : AcceptanceTest
    {
        [Test]
        public async Task Should_publish_notification()
        {
            var externalProcessorSubscribed = false;
            CustomConfiguration = config => config.OnEndpointSubscribed<MyContext>((s, ctx) =>
            {
                if (s.SubscriberReturnAddress.IndexOf("ExternalProcessor", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    externalProcessorSubscribed = true;
                }
            });

            ExecuteWhen(() => externalProcessorSubscribed, async domainEvents =>
            {
                await domainEvents.Raise(new ServiceControl.Contracts.CustomChecks.CustomCheckSucceeded
                {
                    Category = "Testing",
                    CustomCheckId = "Success custom check",
                    OriginatingEndpoint = new EndpointDetails
                    {
                        Host = "MyHost",
                        HostId = Guid.Empty,
                        Name = "Testing"
                    },
                    SucceededAt = DateTime.Now
                });
                await domainEvents.Raise(new ServiceControl.Contracts.CustomChecks.CustomCheckFailed
                {
                    Category = "Testing",
                    CustomCheckId = "Fail custom check",
                    OriginatingEndpoint = new EndpointDetails
                    {
                        Host = "MyHost",
                        HostId = Guid.Empty,
                        Name = "Testing"
                    },
                    FailedAt = DateTime.Now,
                    FailureReason = "Because I can"
                });
            });

            var context = await Define<MyContext>()
                .WithEndpoint<ExternalProcessor>(b => b.When(async (bus, c) =>
                {
                    await bus.Subscribe<CustomCheckSucceeded>();
                    await bus.Subscribe<CustomCheckFailed>();

                    if (c.HasNativePubSubSupport)
                    {
                        externalProcessorSubscribed = true;
                    }
                }))
                .Done(c => c.CustomCheckFailedReceived && c.CustomCheckSucceededReceived)
                .Run();

            Assert.IsTrue(context.CustomCheckFailedReceived);
            Assert.IsTrue(context.CustomCheckSucceededReceived);
        }

        public class ExternalProcessor : EndpointConfigurationBuilder
        {
            public ExternalProcessor()
            {
                EndpointSetup<JsonServer>(c =>
                {
                    var routing = c.ConfigureTransport().Routing();
                    routing.RouteToEndpoint(typeof(MessageFailed).Assembly, Settings.DEFAULT_SERVICE_NAME);
                }, publisherMetadata =>
                {
                    publisherMetadata.RegisterPublisherFor<CustomCheckSucceeded>(Settings.DEFAULT_SERVICE_NAME);
                    publisherMetadata.RegisterPublisherFor<CustomCheckFailed>(Settings.DEFAULT_SERVICE_NAME);
                });
            }

            public class CustomCheckSucceededHandler : IHandleMessages<CustomCheckSucceeded>
            {
                public MyContext Context { get; set; }

                public Task Handle(CustomCheckSucceeded message, IMessageHandlerContext context)
                {
                    Context.CustomCheckSucceededReceived = true;
                    return Task.FromResult(0);
                }
            }

            public class CustomCheckFailedHandler : IHandleMessages<CustomCheckFailed>
            {
                public MyContext Context { get; set; }

                public Task Handle(CustomCheckFailed message, IMessageHandlerContext context)
                {
                    Context.CustomCheckFailedReceived = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class MyContext : ScenarioContext
        {
            public bool CustomCheckSucceededReceived { get; set; }
            public bool CustomCheckFailedReceived { get; set; }
        }
    }
}