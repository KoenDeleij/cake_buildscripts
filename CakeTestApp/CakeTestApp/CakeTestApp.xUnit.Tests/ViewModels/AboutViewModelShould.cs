using System;
using Xunit;
using CakeTestApp.ViewModels;
using FluentAssertions;

namespace CakeTestApp.xUnit.Tests.ViewModels
{
    public class AboutViewModelShould
    {
		private readonly AboutViewModel vm;

		public AboutViewModelShould()
		{
			vm = new AboutViewModel();
		}

        [Fact]
        public void Construct()
		{
			vm.Should().BeOfType<AboutViewModel>();
		}

		[Fact]
        public void HaveTitle()
        {
			vm.Title.Should().Be("About");
        }
    }
}
