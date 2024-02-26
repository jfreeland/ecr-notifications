using System.Net;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.ECR.Model;
using Amazon.Lambda.Core;
using SlackAPI;

namespace ECRWarnings;

public class DynamoUtils
{
    private readonly AmazonDynamoDBClient _dynamoClient;
    private readonly string _dynamoTable;

    public DynamoUtils()
    {
        _dynamoClient = new AmazonDynamoDBClient();
        _dynamoTable = Environment.GetEnvironmentVariable("DYNAMODB_TABLE_NAME") ?? "";
        if (string.IsNullOrEmpty(_dynamoTable))
        {
            throw new Exception("DYNAMODB_TABLE_NAME environment variable not set.");
        }
    }

    public async Task<Dictionary<string, AttributeValue>>? GetFindingRecord(ILambdaContext context, string key, string repositoryName)
    {
        var request = new GetItemRequest
        {
            TableName = _dynamoTable,
            Key = new Dictionary<string, AttributeValue>
            {
                { "key", new AttributeValue { S = key } }
            }
        };

        var response = await _dynamoClient.GetItemAsync(request);
        return response.Item;
    }

    public async Task<bool> PutFindingRecord(ILambdaContext context, string repository, string imageId, ImageScanFinding finding)
    {
        var request = new PutItemRequest
        {
            TableName = _dynamoTable,
            Item = new Dictionary<string, AttributeValue>
            {
                { "key", new AttributeValue { S = $"{repository}#{finding.Name}" } },
                { "repository", new AttributeValue { S = repository } },
                { "imageId", new AttributeValue { S = imageId } },
                { "name", new AttributeValue { S = finding.Name } },
                { "description", new AttributeValue { S = finding.Description } },
                { "uri", new AttributeValue { S = finding.Uri } }
            }
        };

        var response = await _dynamoClient.PutItemAsync(request);
        if (response.HttpStatusCode != HttpStatusCode.OK)
        {
            context.Logger.LogError($"Error putting finding record for \"{repository}#{imageId}#{finding.Name}\": HTTP response code: {response.HttpStatusCode}");
            return false;
        }
        return true;
    }
}