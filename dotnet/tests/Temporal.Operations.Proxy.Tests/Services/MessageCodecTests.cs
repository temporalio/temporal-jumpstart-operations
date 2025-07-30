using System.Text;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Temporal.Operations.Proxy.Interfaces;
using Temporal.Operations.Proxy.Models;
using Temporal.Operations.Proxy.Services;
using Temporalio.Api.Common.V1;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.History.V1;
using Temporalio.Api.TaskQueue.V1;
using Temporalio.Api.Update.V1;
using Temporalio.Api.WorkflowService.V1;

namespace Temporal.Operations.Proxy.Tests.Services;

public class MessageCodecTest : IClassFixture<TemporalApiDescriptorFixture>
{
    private readonly ICodec<PayloadContext, byte[]> _encoder;
    private readonly string _defaultNamespace;
    private readonly AesByteEncryptor _encryptor;
    private readonly string _keyId;
    private readonly MessageCodec _sut;
    public MessageCodecTest(TemporalApiDescriptorFixture fixture)
    {
        var apiDescriptor = fixture.TemporalApiDescriptor;

        _defaultNamespace = "default";
        _keyId = "TestKeyId";
        var keyResolver = new InMemoryTemporalNamespaceKeyIdResolver();
        keyResolver.AddKeyId(_defaultNamespace, _keyId);
        _encryptor = new AesByteEncryptor();
        _encryptor.AddKey(_keyId, new byte[32]);

        _encoder = new CryptPayloadCodec(_encryptor, keyResolver);

        _sut = new MessageCodec(apiDescriptor, _encoder, new Logger<MessageCodec>(new LoggerFactory()));
    }

    [Fact]
    public async Task GivenStartWorkflowExecution_ItShouldTransformRequest()
    {
        var payload = new Temporalio.Api.Common.V1.Payload
        {
            Metadata =
            {
                ["encoding"] = ByteString.CopyFromUtf8("json/plain")
            },
            Data = ByteString.CopyFromUtf8("{\"message\": \"Hello World\"}")
        };

        var request = new StartWorkflowExecutionRequest
        {
            Namespace = "default",
            WorkflowId = "test-workflow-123",
            WorkflowType = new WorkflowType { Name = "MyWorkflow" },
            TaskQueue = new TaskQueue { Name = "my-task-queue" },
            Input = new Payloads
            {
                Payloads_ = { payload }
            },
            WorkflowExecutionTimeout = Duration.FromTimeSpan(TimeSpan.FromMinutes(10)),
            WorkflowRunTimeout = Duration.FromTimeSpan(TimeSpan.FromMinutes(5)),
            Identity = "my-worker-identity",
            RequestId = Guid.NewGuid().ToString(),
            WorkflowIdReusePolicy = WorkflowIdReusePolicy.AllowDuplicate,
            SearchAttributes = new SearchAttributes
            {
                IndexedFields =
                {
                    { "custom-field", new Payload
                    {
                        Metadata = { { "encoding", ByteString.CopyFromUtf8("json/plain") } },
                        Data = ByteString.CopyFromUtf8("{\"message\": \"Hello SA\"}")
                    } }
                }
            }
        };
        request.WorkflowTaskTimeout = Duration.FromTimeSpan(TimeSpan.FromSeconds(30));
        var grpcRequest = GrpcUtils.CreateGrpcFrame(request.ToByteArray());


        using var stream = new MemoryStream();
        stream.Write(grpcRequest, 0, grpcRequest.Length);
        var ctx = GrpcUtils.CreateGrpcHttpContext(
            "/temporal.api.workflowservice.v1.WorkflowService/StartWorkflowExecution",
            grpcRequest);
        Assert.True(stream.Length > 0);
        stream.Position = 0; // Reset position before assigning to request body
        ctx.Request.Body = stream;
        ctx.Request.ContentLength = grpcRequest.Length;
        var temporalContext = new TemporalContext
        {
            Namespace = _defaultNamespace,
            RequestMessageTypeName = ((IMessage)request).Descriptor.FullName,
            ResponseMessageTypeName = "temporal.api.workflowservice.v1.StartWorkflowExecutionResponse",
            Path = Guid.NewGuid().ToString(),
        };
        var messageContext = new MessageContext
        {
            MessageTypeName = temporalContext.RequestMessageTypeName,
            TemporalContext = temporalContext,
        };
        var transformed = await _sut.EncodeAsync(messageContext, stream.ToArray()[5..]);
        using var actualStream = new MemoryStream(transformed);
        var actual = StartWorkflowExecutionRequest.Parser.ParseFrom(actualStream);
        var actualPayload = actual.Input.Payloads_[0];
        Assert.Single(actual.Input.Payloads_); ;
        Assert.True(actualPayload.Metadata.ContainsKey("encryption-key-id"));
        Assert.Equal(ByteString.CopyFromUtf8(CryptPayloadCodec.EncodingMetadataValue), actualPayload.Metadata["encoding"]);
        Assert.True(_encryptor.Decrypt(_keyId, actualPayload.Data.ToByteArray()).SequenceEqual(payload.Data), "failed to decrypt transformed .Data");

        Assert.False(actual.SearchAttributes.IndexedFields["custom-field"].Metadata.ContainsKey("encryption-key-id")); ;
    }

    [Fact]
    public async Task GivenUpdateWorkflowExecutionRequest()
    {
        var payload = new Temporalio.Api.Common.V1.Payload
        {
            Metadata =
            {
                ["encoding"] = ByteString.CopyFromUtf8("json/plain")
            },
            Data = ByteString.CopyFromUtf8("{\"message\": \"Hello World\"}")
        };
        var header = new Temporalio.Api.Common.V1.Payload
        {
            Metadata =
            {
                ["encoding"] = ByteString.CopyFromUtf8("json/sexy")
            },
            Data = ByteString.CopyFromUtf8("{\"message\": \"Hello Header\"}")
        };
        var request = new UpdateWorkflowExecutionRequest
        {

            FirstExecutionRunId = Guid.NewGuid().ToString(),
            Namespace = "default",
            Request = new Request
            {
                Input = new Input
                {
                    Args = new Payloads
                    {
                        Payloads_ = { payload }
                    },
                    Name = "myupdate",
                    Header = new Header
                    {
                        Fields = { { "custom", header } }
                    },
                }
            },
            WaitPolicy = new WaitPolicy
            {
                LifecycleStage = UpdateWorkflowExecutionLifecycleStage.Completed,
            },
            WorkflowExecution = new WorkflowExecution()
            {
                WorkflowId = Guid.NewGuid().ToString(),
                RunId = Guid.NewGuid().ToString(),
            }
        };

        var grpcRequest = GrpcUtils.CreateGrpcFrame(request.ToByteArray());

        using var stream = new MemoryStream();
        stream.Write(grpcRequest, 0, grpcRequest.Length);
        var ctx = GrpcUtils.CreateGrpcHttpContext(
            "/temporal.api.workflowservice.v1.WorkflowService/UpdateWorkflowExecution",
            grpcRequest);
        Assert.True(stream.Length > 0);
        stream.Position = 0; // Reset position before assigning to request body
        ctx.Request.Body = stream;
        ctx.Request.ContentLength = grpcRequest.Length;
        var temporalContext = new TemporalContext
        {
            Namespace = _defaultNamespace,
            RequestMessageTypeName = ((IMessage)request).Descriptor.FullName,
            ResponseMessageTypeName = "temporal.api.workflowservice.v1.UpdateWorkflowExecutionResponse",
            Path = Guid.NewGuid().ToString(),
        };
        var messageContext = new MessageContext
        {
            MessageTypeName = temporalContext.RequestMessageTypeName,
            TemporalContext = temporalContext,
        };
        var transformed = await _sut.EncodeAsync(messageContext, stream.ToArray()[5..]);
        using var actualStream = new MemoryStream(transformed);
        var actual = UpdateWorkflowExecutionRequest.Parser.ParseFrom(actualStream);
        var actualInputArgsPayload = actual.Request.Input.Args.Payloads_[0];
        var actualInputHeaderPayload = actual.Request.Input.Header.Fields["custom"];
        Assert.Single(actual.Request.Input.Args.Payloads_); ;
        Assert.True(actualInputArgsPayload.Metadata.ContainsKey("encryption-key-id"));
        Assert.Equal(ByteString.CopyFromUtf8(CryptPayloadCodec.EncodingMetadataValue), actualInputArgsPayload.Metadata["encoding"]);
        Assert.True(_encryptor.Decrypt(_keyId, actualInputArgsPayload.Data.ToByteArray()).SequenceEqual(payload.Data), "failed to decrypt transformed .Data");

        Assert.True(actualInputHeaderPayload.Metadata.ContainsKey("encryption-key-id"));
        Assert.Equal(ByteString.CopyFromUtf8(CryptPayloadCodec.EncodingMetadataValue), actualInputHeaderPayload.Metadata["encoding"]);
        Assert.Equal(ByteString.CopyFromUtf8("json/sexy"), actualInputHeaderPayload.Metadata["encoding-original"]);
        Assert.True(_encryptor.Decrypt(_keyId, actualInputHeaderPayload.Data.ToByteArray()).SequenceEqual(header.Data), "failed to decrypt transformed .Data");
    }

    [Fact]
    public async Task GivenPayloadsInExecutionHistoryResponse()
    {

        // Create ENCRYPTED payloads (simulating what comes from Temporal server)
        var payloads = new Payloads();
        var originalPlainDataList = new List<byte[]>(); // Keep track of original plain data

        for (int i = 0; i < 10; i++)
        {
            var plainData = Encoding.UTF8.GetBytes("{\"message\": \"Hello World-" + i + "\"}");
            originalPlainDataList.Add(plainData);

            // Create ENCRYPTED payloads (as they would come from Temporal server)
            payloads.Payloads_.Add(new Temporalio.Api.Common.V1.Payload
            {
                Metadata =
                {
                    ["encoding-original"] = ByteString.CopyFromUtf8("json/plain"),
                    ["version"] = ByteString.CopyFromUtf8("1.0.0"),
                    ["encryption-key-id"] = ByteString.CopyFromUtf8(_keyId),
                    ["encoding"] = ByteString.CopyFromUtf8(CryptPayloadCodec.EncodingMetadataValue)
                },
                Data = ByteString.CopyFrom(_encryptor.Encrypt(_keyId, plainData)) // Encrypted data
            });
        }

        var response = new GetWorkflowExecutionHistoryResponse
        {
            History = new History()
        };

        response.History.Events.Add(new HistoryEvent
        {
            EventId = 1,
            EventTime = Timestamp.FromDateTime(DateTime.UtcNow),
            EventType = EventType.WorkflowExecutionStarted,
            TaskId = 1,
            Version = 1,
            WorkflowExecutionStartedEventAttributes = new WorkflowExecutionStartedEventAttributes
            {
                Attempt = 1,
                WorkflowId = Guid.NewGuid().ToString(),
                Input = payloads, // This contains ENCRYPTED payloads
                FirstExecutionRunId = Guid.NewGuid().ToString(),
                Identity = Guid.NewGuid().ToString(),
                TaskQueue = new TaskQueue
                {
                    Kind = TaskQueueKind.Normal,
                    Name = Guid.NewGuid().ToString(),
                },
                WorkflowType = new WorkflowType
                {
                    Name = Guid.NewGuid().ToString(),
                },
                WorkflowExecutionTimeout = Duration.FromTimeSpan(TimeSpan.FromSeconds(5)),
                WorkflowRunTimeout = Duration.FromTimeSpan(TimeSpan.FromSeconds(5)),
                WorkflowTaskTimeout = Duration.FromTimeSpan(TimeSpan.FromSeconds(5)),
                Initiator = ContinueAsNewInitiator.Unspecified,
                SearchAttributes = new SearchAttributes(),
                Header = new Header(),
            }
        });
        response.History.Events.Add(new HistoryEvent()
        {
            EventId = 2,  // Required - sequential event ID
            EventTime = Timestamp.FromDateTime(DateTime.UtcNow),  // Required
            EventType = EventType.ActivityTaskScheduled,  // Required - must match the attributes
            TaskId = 2,  // Required
            Version = 1,  // Required
            ActivityTaskScheduledEventAttributes = new ActivityTaskScheduledEventAttributes
            {
                ActivityId = Guid.NewGuid().ToString(),
                ActivityType = new ActivityType
                {
                    Name = "ProcessPaymentActivity"  // Use a meaningful name instead of GUID
                },
                Input = payloads,  // Your Payloads object
                TaskQueue = new TaskQueue
                {
                    Kind = TaskQueueKind.Normal,
                    Name = "payment-task-queue"  // Use a meaningful name
                },
                StartToCloseTimeout = Duration.FromTimeSpan(TimeSpan.FromSeconds(30)),

                // Optional but commonly used fields:
                ScheduleToCloseTimeout = Duration.FromTimeSpan(TimeSpan.FromMinutes(5)),
                ScheduleToStartTimeout = Duration.FromTimeSpan(TimeSpan.FromMinutes(1)),
                HeartbeatTimeout = Duration.FromTimeSpan(TimeSpan.FromSeconds(10)),

                // Optional Header with payload data (if needed):
                Header = new Header
                {
                    Fields =
                        {
                            {
                                "correlation-id",
                                new Temporalio.Api.Common.V1.Payload
                                {
                                    Metadata = { { "encoding", ByteString.CopyFromUtf8("json/plain") } },
                                    Data = ByteString.CopyFrom(_encryptor.Encrypt(_keyId,Encoding.UTF8.GetBytes("\"correlation-12345\"")))
                                }
                            }
                        }
                },

                // Optional retry policy:
                RetryPolicy = new RetryPolicy
                {
                    MaximumAttempts = 3,
                    InitialInterval = Duration.FromTimeSpan(TimeSpan.FromSeconds(1)),
                    MaximumInterval = Duration.FromTimeSpan(TimeSpan.FromMinutes(1)),
                    BackoffCoefficient = 2.0
                }
            }
        });
        var grpcResponse = GrpcUtils.CreateGrpcFrame(response.ToByteArray());

        var temporalContext = new TemporalContext
        {
            Namespace = _defaultNamespace,
            ResponseMessageTypeName = ((IMessage)response).Descriptor.FullName,
            RequestMessageTypeName = "temporal.api.workflowservice.v1.GetWorkflowExecutionHistoryRequest",
            Path = Guid.NewGuid().ToString(),
        };
        var messageContext = new MessageContext
        {
            MessageTypeName = temporalContext.ResponseMessageTypeName,
            TemporalContext = temporalContext,
        };
        // Transform the RESPONSE (this will DECRYPT the payloads)
        var transformed = await _sut.DecodeAsync(messageContext, grpcResponse[5..]);

        // Parse the transformed result
        using var actualStream = new MemoryStream(transformed);
        var actual = GetWorkflowExecutionHistoryResponse.Parser.ParseFrom(actualStream);
        var actualInputArgsPayloads = actual.History.Events[0].WorkflowExecutionStartedEventAttributes.Input.Payloads_;

        // Assertions
        Assert.Equal(10, actualInputArgsPayloads.Count);

        for (int i = 0; i < actualInputArgsPayloads.Count; i++)
        {
            var decryptedPayload = actualInputArgsPayloads[i];
            var originalPlainData = originalPlainDataList[i];

            // Check that encryption metadata was removed/changed
            Assert.Equal(ByteString.CopyFromUtf8("json/plain"), decryptedPayload.Metadata["encoding"]);
            Assert.False(decryptedPayload.Metadata.ContainsKey("encryption-key-id")); // Should be removed after decryption

            // Check that the data was decrypted back to original plain text
            Assert.True(decryptedPayload.Data.ToByteArray().SequenceEqual(originalPlainData),
                $"Payload {i} was not decrypted correctly");
        }
    }
}