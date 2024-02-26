using Amazon.CDK;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Constructs;
using System;

namespace ECRNotifications
{
    public class ECRNotificationsStack : Stack
    {
        internal ECRNotificationsStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            var _slackAPIToken = System.Environment.GetEnvironmentVariable("SLACK_BOT_USER_OAUTH_TOKEN");
            if (string.IsNullOrEmpty(_slackAPIToken))
            {
                throw new System.Exception("SLACK_BOT_USER_OAUTH_TOKEN environment variable is required");
            }

            var _tableName = "ECRVulnerabiltyScanFindings";
            var findingsTable = new Table(this, _tableName, new TableProps
            {
                PartitionKey = new Amazon.CDK.AWS.DynamoDB.Attribute
                {
                    Name = "key",
                    Type = AttributeType.STRING
                },
                BillingMode = BillingMode.PAY_PER_REQUEST,
                RemovalPolicy = RemovalPolicy.DESTROY,
            });

            var buildOption = new BundlingOptions()
            {
                Image = Runtime.DOTNET_8.BundlingImage,
                User = "root",
                OutputType = BundlingOutput.ARCHIVED,
                Command = new string[] {
                    "/bin/sh",
                    "-c",
                    " dotnet tool install -g Amazon.Lambda.Tools" +
                    " && dotnet build" +
                    " && dotnet lambda package --output-package /asset-output/function.zip"
                }
            };

            var ecrNotificationFunction = new Function(this, "ECRNotificationFunction", new FunctionProps
            {
                Runtime = Runtime.DOTNET_8,
                Timeout = Duration.Seconds(30),
                MemorySize = 256,
                LogRetention = RetentionDays.ONE_DAY,
                Handler = "ECRWarnings::ECRWarnings.Function::FunctionHandler",
                Code = Code.FromAsset("apps/src/ECRWarnings", new Amazon.CDK.AWS.S3.Assets.AssetOptions
                {
                    Bundling = buildOption
                }),
                Environment = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "SLACK_API_TOKEN", _slackAPIToken }, // TODO: Put this in SSM instead of environment.
                    { "DYNAMODB_TABLE_NAME", findingsTable.TableName }
                }
            });

            ecrNotificationFunction.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new string[] {
                    "ecr:DescribeImages",
                    "ecr:DescribeImageScanFindings", // Added missing comma here
                    "ecr:DescribeRepositories",
                    "ecr:GetAuthorizationToken",
                    "ecr:ListImages",
                },
                Resources = new string[] { "*" }
            }));
            ecrNotificationFunction.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new string[] {
                    "dynamodb:BatchGet*",
                    "dynamodb:Get*",
                    "dynamodb:Query",
                    "dynamodb:Scan",
                    "dynamodb:BatchWrite*",
                    "dynamodb:Delete*",
                    "dynamodb:Update*",
                    "dynamodb:PutItem"
                },
                Resources = new string[] { findingsTable.TableArn }
            }));
        }
    }
}