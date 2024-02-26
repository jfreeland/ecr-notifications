using Amazon.ECR;
using Amazon.ECR.Model;
using Amazon.Lambda.Core;

namespace ECRWarnings;

public class ECRUtils
{
    private readonly AmazonECRClient _ecrClient;

    public ECRUtils()
    {
        _ecrClient = new AmazonECRClient();
    }

    public async Task<List<string>?> GetRepositories(ILambdaContext context)
    {
        var describeRepositoriesRequest = new DescribeRepositoriesRequest { };
        try
        {
            var response = await _ecrClient.DescribeRepositoriesAsync(describeRepositoriesRequest);
            return response.Repositories?.Select(repo => repo.RepositoryName).ToList();
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error getting repositories: {ex.Message}");
            return null;
        }
    }

    public async Task<string?> GetMostRecentImageTag(ILambdaContext context, string repositoryName)
    {
        var request = new ListImagesRequest
        {
            RepositoryName = repositoryName
        };

        var response = await _ecrClient.ListImagesAsync(request);
        var imageIds = response.ImageIds;

        var mostRecentImage = imageIds
            .Select(async imageId => new
            {
                ImageId = imageId,
                ImageDetail = await GetImageDetail(context, repositoryName, imageId)
            })
            .Select(task => task.Result)
            .OrderByDescending(image => image.ImageDetail?.ImagePushedAt)
            .FirstOrDefault();

        return mostRecentImage?.ImageId.ImageTag;
    }

    private async Task<ImageDetail?> GetImageDetail(ILambdaContext context, string repositoryName, ImageIdentifier imageId)
    {
        var describeImagesRequest = new DescribeImagesRequest
        {
            RepositoryName = repositoryName,
            ImageIds = [imageId]
        };

        var describeImagesResponse = await _ecrClient.DescribeImagesAsync(describeImagesRequest);
        return describeImagesResponse.ImageDetails?.FirstOrDefault();
    }

    public async Task<ImageScanFindings?> GetImageScanFindings(ILambdaContext context, string repositoryName, ImageIdentifier imageId)
    {
        var describeImageScanFindingsRequest = new DescribeImageScanFindingsRequest
        {
            RepositoryName = repositoryName,
            ImageId = imageId
        };

        var response = await _ecrClient.DescribeImageScanFindingsAsync(describeImageScanFindingsRequest);
        return response.ImageScanFindings;
    }
}