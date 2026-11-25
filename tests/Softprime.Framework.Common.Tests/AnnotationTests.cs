using System;
using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Softprime.Framework.Common.Annotations;
using Softprime.Framework.Common.Extensions;
using Xunit;

namespace Softprime.Framework.Common.Tests
{
    public class AnnotationTests
    {
        [Theory]
        [InlineData("529.982.247-25", true)]
        [InlineData("529.982.247-24", false)]
        [InlineData("111.111.111-11", false)]
        [InlineData("04.252.011/0001-10", true)]
        [InlineData("04.252.011/0001-11", false)]
        [InlineData("00.000.000/0000-00", true)]
        [InlineData("123", false)]
        [InlineData("", true)]
        [InlineData(null, true)]
        public void CpfCnpj_FormattedAndOptionalValues_UsesLegacyCheckDigits(string value, bool expected)
        {
            var annotation = new CpfCnpjAttribute();

            annotation.IsValid(value).Should().Be(expected);
        }

        [Fact]
        public void CpfAndCnpj_DigitStrings_ValidateByDocumentType()
        {
            "52998224725".IsCpf().Should().BeTrue();
            "52998224724".IsCpf().Should().BeFalse();
            "04252011000110".IsCnpj().Should().BeTrue();
            "04252011000111".IsCnpj().Should().BeFalse();
        }

        [Theory]
        [InlineData(false, null, true)]
        [InlineData(true, null, false)]
        [InlineData(true, " ", false)]
        [InlineData(true, "synthetic", true)]
        public void RequiredIf_MatchingDependentProperty_AppliesRequired(
            bool enabled, string value, bool valid)
        {
            var input = new Input { Enabled = enabled, Label = value };
            var context = new ValidationContext(input) { MemberName = nameof(Input.Label) };
            var attribute = new RequiredIfAttribute(nameof(Input.Enabled), true, "{0} is required");

            var result = attribute.GetValidationResult(input.Label, context);

            if (valid)
                result.Should().BeNull();
            else
            {
                result.ErrorMessage.Should().Be("Label is required");
                result.MemberNames.Should().Equal(nameof(Input.Label));
            }
        }

        [Fact]
        public void RequiredIf_MissingDependentProperty_Throws()
        {
            var context = new ValidationContext(new Input()) { MemberName = nameof(Input.Label) };
            var attribute = new RequiredIfAttribute("Unknown", true);

            Action validate = () => attribute.GetValidationResult(null, context);

            validate.Should().Throw<MissingMemberException>();
        }

        private sealed class Input
        {
            public bool Enabled { get; set; }
            public string Label { get; set; }
        }
    }
}
