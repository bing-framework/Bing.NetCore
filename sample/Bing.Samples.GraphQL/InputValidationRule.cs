using GraphQL.Validation;

namespace Bing.Samples.GraphQL
{
    public class InputValidationRule:IValidationRule
    {
        public INodeVisitor Validate(ValidationContext context)
        {
            return new EnterLeaveListener(_ => { });
        }
    }
}
