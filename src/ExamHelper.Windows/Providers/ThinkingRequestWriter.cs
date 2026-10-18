using ExamHelper.Core.Common;
using ExamHelper.Core.Settings;
namespace ExamHelper.Windows.Providers;
public static class ThinkingRequestWriter
{
    public static void Apply(IDictionary<string,object> payload,AiSettings settings)
    {
        var choice = settings.Thinking; var capability = settings.ThinkingCapability;
        if(choice.Kind == ThinkingKind.ServiceDefault) return;
        if(!capability.Allows(choice)) throw new ServiceFailure(FailureCategory.InvalidPayload,"当前模型不支持所选思考设置。");
        if(choice.Kind == ThinkingKind.Effort && capability.WireFormat is ThinkingWireFormat.ReasoningEffort or ThinkingWireFormat.ThinkingObjectAndEffort)
        {
            payload["reasoning_effort"] = choice.Effort!;
            if(capability.WireFormat == ThinkingWireFormat.ThinkingObjectAndEffort) payload["thinking"] = new { type = "enabled" };
        }
        else if(choice.Kind is ThinkingKind.Enabled or ThinkingKind.Disabled && capability.WireFormat is ThinkingWireFormat.ThinkingObject or ThinkingWireFormat.ThinkingObjectAndEffort)
            payload["thinking"] = new { type = choice.Kind == ThinkingKind.Enabled ? "enabled" : "disabled" };
        else throw new ServiceFailure(FailureCategory.InvalidPayload,"思考设置和请求方式不匹配。");
    }
}
