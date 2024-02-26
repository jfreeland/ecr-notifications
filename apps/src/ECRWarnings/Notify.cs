using SlackAPI;
using Amazon.Lambda.Core;
using Amazon.ECR.Model;

namespace ECRWarnings;

public class Notify
{
    public Notify() { }

    public static async Task SendSlackMessage(ILambdaContext context, string repository, string imageId, ImageScanFinding finding)
    {
        var notificationChannel = "#ecr";
        var slackClient = new SlackTaskClient(Environment.GetEnvironmentVariable("SLACK_API_TOKEN") ?? "");
        var message = $"Critical finding for \"{repository}:{imageId}\" name: {finding.Name}\n" +
                      $"desc: {finding.Description}\n" +
                      $"uri: {finding.Uri}";
        var response = await slackClient.PostMessageAsync(notificationChannel, message);
        if (!response.ok)
        {
            context.Logger.LogError($"Error sending message to Slack: {response.error}");
        }
    }
}