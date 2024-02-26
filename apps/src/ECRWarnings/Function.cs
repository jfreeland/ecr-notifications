using Amazon.DynamoDBv2.Model;
using Amazon.ECR;
using Amazon.ECR.Model;
using Amazon.Lambda.Core;

namespace ECRWarnings;

public class Function
{
    private readonly AmazonECRClient _ecrClient;
    private readonly ECRUtils _ecrUtils;
    private readonly DynamoUtils _dynamoUtils;

    public Function()
    {
        _ecrClient = new AmazonECRClient();
        _ecrUtils = new ECRUtils();
        _dynamoUtils = new DynamoUtils();
    }

    public async Task FunctionHandler(ILambdaContext context)
    {
        context.Logger.LogInformation("Starting.");

        var repositories = await _ecrUtils.GetRepositories(context);
        if (repositories != null)
        {
            foreach (var repository in repositories)
            {
                context.Logger.LogInformation($"Looking for images in repository {repository}");
                var imageId = await _ecrUtils.GetMostRecentImageTag(context, repository);
                if (imageId != null)
                {
                    context.Logger.LogInformation($"Most recent image tag for repository {repository} is {imageId}");
                    var imageScanFindings = await _ecrUtils.GetImageScanFindings(context, repository, new ImageIdentifier { ImageTag = imageId });
                    if (imageScanFindings != null)
                    {
                        foreach (var finding in imageScanFindings.Findings)
                        {
                            if (finding.Severity == "CRITICAL")
                            {
                                context.Logger.LogInformation($"Critical finding for \"{repository}:{imageId}\" name: {finding.Name}");
                                context.Logger.LogInformation($"Critical finding for \"{repository}:{imageId}\" desc: {finding.Description}");
                                context.Logger.LogInformation($"Critical finding for \"{repository}:{imageId}\"  uri: {finding.Uri}");

                                if (finding.Name != null)
                                {
                                    var item = await _dynamoUtils.GetFindingRecord(context, $"{repository}#{finding.Name}", repository);
                                    if (item.Count == 0)
                                    {
                                        context.Logger.LogInformation($"Did not find a record for finding {finding.Name}");
                                    }
                                    else
                                    {
                                        // TODO: This could be a lot more robust.
                                        context.Logger.LogInformation($"Found an existing record for finding {finding.Name}");
                                        continue;
                                    }
                                    var putFindingRecord = await _dynamoUtils.PutFindingRecord(context, repository, imageId, finding);
                                    if (putFindingRecord)
                                    {
                                        context.Logger.LogInformation($"Sending Slack message for {finding.Name}");
                                        await Notify.SendSlackMessage(context, repository, imageId, finding);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        context.Logger.LogError($"Error getting image scan results for repository {repository} image {imageId}");
                    }
                }
                else
                {
                    context.Logger.LogInformation($"Did not find an image tag for repository {repository}");
                }
            }
        }
        context.Logger.LogInformation("Done.");
    }
}