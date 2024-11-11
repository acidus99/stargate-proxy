using RocketForce;

namespace Stargate.Transformers;

public class ResponseTransformer
{
    private readonly ITransformer[] transformers =
    [
        new HtmlTransformer(),
        new FeedTransformer(),
        new ImageTransformer()
    ];

    public SourceResponse Transform(Request request, SourceResponse original)
    {
        try
        {
            foreach (var transformer in transformers)
                if (transformer.CanTransform(original.Meta))
                    return transformer.Transform(request, original);
            return original;
        }
        catch (TransformationException ex)
        {
            return new SourceResponse
            {
                StatusCode = 20,
                Meta = $"Error transforming content ({ex.Message})"
            };
        }
    }
}