using System.Windows;
using System.Windows.Controls;
using ExamHelper.App.Help;
using ExamHelper.Core.Settings;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public sealed class UsageContent04Tests
{
    [Fact] public async Task UsageSectionsHaveBoldHeadingsAndCurrentRules()
    {
        await StaTest.RunAsync(()=>
        {
            var window=new UsageWindow(HotkeyBinding.Defaults);window.Show();window.UpdateLayout();StaTest.Pump();
            try
            {
                Assert.True(window.Sections.Count>=9);
                foreach(var section in window.Sections)Assert.Contains(MarkdownViewTests.Nodes(window).OfType<TextBlock>(),t=>t.Text==section.Title && t.FontWeight==FontWeights.Bold);
                foreach(var token in new[] { "空白","文字测试","图片测试","右上角","批量","Markdown","每次重试","仅供学习","HTML","Shift + Enter" })Assert.Contains(token,window.Instructions);
                Assert.DoesNotContain("点击待发送缩略图删除",window.Instructions);Assert.DoesNotContain("语音服务须通过识别测试",window.Instructions);
            }
            finally { window.Close();StaTest.Pump(); }
        });
    }
}
