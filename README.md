# Temporal Jumpstart Operations

### **WIP** .NET Standalone Proxy

See [Temporal.Operations.Proxy](/dotnet) for the Solution.

This proxy is a [YARP](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/yarp-overview?view=aspnetcore-9.0) service that sits between your Temporal SDK clients and Temporal Cloud.

* There are no Temporal SDK dependencies
* There is minimal Unmarshal/Marshal of the grpc bytes.


#### Running With The Proxy

0. Start the Temporal Dev Server
1. Start the proxy 
```
dotnet run --project dotnet/src/Temporal.Operations.Proxy
# this should start on https://localhost:5000
# note that this is SSL/TLS!
```

2. Start a workflow
```
temporal workflow start --tls \
	--address localhost:5000 \
	--workflow-id foo \
	--type MyWorkflow \
	--task-queue apps \
	--input '{"bonk":"boop"}'
```
3. Check Temporal UI and note the headers in the input:
* `encryption-key-id`
* `encoding-original`
* `encoding`

4. The `data` should be encrypted 
5. Now get the Workflow history and zoom in on the Workflow Execution Started Event to get the payload 
```
temporal workflow show --tls \
	--address localhost:5000 \
	--workflow-id foo \
	--output | jq '.events[0].workflowExecutionStartedEventAttributes.input.payloads[0]'
```
6. This should return something like
```
{
  "metadata": {
    "encoding": "anNvbi9wbGFpbg=="
  },
  "data": "eyJib25rIjoiYm9vcCJ9"
}
```
7. Notice:
	a. The additional `metadata` fields are not there..just the original metadata
	b. The data is shorter

8. Now decode the metadata and the data fields. They should be 
the original `input` you sent in!
```
echo "anNvbi9wbGFpbg==" | base64 --decode
# returns `json/plain`
echo "eyJib25rIjoiYm9vcCJ9" | base64 --decode
# returns `{"bonk":"boop"}`
```


