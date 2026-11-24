using System;
using System.IO;
using System.Reflection;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Softprime.Framework.Api.Dto.Default;
using Xunit;
#if NET10_0
using Softprime.Framework.Api.Tests;
#endif

namespace Softprime.Framework.Api.Dto.Tests
{
    public class DtoJsonTests : IClassFixture<LegacyDtoFixture>
    {
        private readonly LegacyDtoFixture fixture;

        public DtoJsonTests(LegacyDtoFixture fixture)
        {
            this.fixture = fixture;
        }

        [Fact]
        public void LegacySerializer_LoadedAssembly_MatchesThePackagedVersion() =>
            fixture.Verify(VerifyPackagedVersion);

        [Fact]
        public void Serialize_DtoWithNullableFields_WritesPascalCaseNamesAndNumericEnum() =>
            fixture.Verify(VerifyNullableFields);

        [Theory]
        [InlineData("""{"Id":7,"Status":2}""", null)]
        [InlineData("""{"Id":7,"Status":2,"Note":null}""", null)]
        [InlineData("""{"Id":7,"Status":2,"Note":"synthetic"}""", "synthetic")]
        public void Deserialize_OptionalFields_HandlesOmissionNullAndText(string json, string note) =>
            fixture.Verify(VerifyOptionalFields, json, note);

        [Fact]
        public void SerializeAndDeserialize_PagedResult_PreserveTheFullPageShape() =>
            fixture.Verify(VerifyPagedResult);

        [Fact]
        public void Deserialize_PageResults_SeparatesNullFromEmptyArray() =>
            fixture.Verify(VerifyPageResults);

        private static void VerifyPackagedVersion()
        {
            var version = LegacyJson.SerializerAssembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

            version.Should().StartWith("12.0.3");
        }

        private static void VerifyNullableFields()
        {
            IDto<int?> dto = new ItemDto { Id = null, Name = "ação", Status = ItemStatus.Active };

            var serialized = LegacyJson.Serialize(dto);

            JToken.DeepEquals(JToken.Parse(serialized),
                JObject.Parse("""{"Id":null,"Name":"ação","Note":null,"Status":2}""")).Should().BeTrue();
        }

        private static void VerifyOptionalFields(string json, string note)
        {
            IDto<int?> dto = LegacyJson.Deserialize<ItemDto>(json);

            dto.Id.Should().Be(7);
            var item = dto.Should().BeOfType<ItemDto>().Which;
            item.Status.Should().Be(ItemStatus.Active);
            item.Name.Should().BeNull();
            item.Note.Should().Be(note);
        }

        private static void VerifyPagedResult()
        {
            IPagedResult<ItemDto> page = new PagedResultDto<ItemDto>
            {
                PageIndex = 1,
                PageSize = 2,
                TotalCount = 5,
                TotalPages = 3,
                Results = new[] { new ItemDto { Id = 7, Name = "sample", Status = ItemStatus.Active } },
                HasPreviousPage = true,
                HasNextPage = true
            };
            const string wireJson = """
                {
                    "PageIndex":1,"PageSize":2,"TotalCount":5,"TotalPages":3,
                    "Results":[{"Id":7,"Name":"sample","Note":null,"Status":2}],
                    "HasPreviousPage":true,"HasNextPage":true
                }
                """;

            var serialized = LegacyJson.Serialize(page);
            IPagedResult<ItemDto> restored = LegacyJson.Deserialize<PagedResultDto<ItemDto>>(wireJson);

            JToken.DeepEquals(JToken.Parse(serialized), JObject.Parse(wireJson)).Should().BeTrue();
            restored.Should().BeEquivalentTo(page);
        }

        private static void VerifyPageResults()
        {
            var empty = LegacyJson.Deserialize<PagedResultDto<ItemDto>>("""{"Results":[]}""");
            var missing = LegacyJson.Deserialize<PagedResultDto<ItemDto>>("""{"Results":null}""");

            empty.Results.Should().BeEmpty();
            missing.Results.Should().BeNull();
            empty.HasNextPage.Should().BeFalse();
            missing.TotalCount.Should().Be(0);
        }

        private enum ItemStatus
        {
            Unknown,
            Active = 2
        }

        private class ItemDto : DtoBase<int?>
        {
            public string Name { get; set; }
            public string Note { get; set; }
            public ItemStatus Status { get; set; }
        }

        private static class LegacyJson
        {
#if NET10_0
            internal static Assembly SerializerAssembly { get; } = new LegacyNewtonsoftLoadContext().LoadSerializer();
#else
            internal static Assembly SerializerAssembly { get; } = Assembly.Load(File.ReadAllBytes(
                Path.Combine(AppContext.BaseDirectory, "legacy", "Newtonsoft.Json.dll")));
#endif

            private static readonly Type JsonConvertType = SerializerAssembly.GetType(
                typeof(JsonConvert).FullName, throwOnError: true);

            internal static readonly Func<object, string> Serialize = (Func<object, string>)JsonConvertType
                .GetMethod(nameof(JsonConvert.SerializeObject), new[] { typeof(object) })
                .CreateDelegate(typeof(Func<object, string>));

            private static readonly Func<string, Type, object> DeserializeObject = (Func<string, Type, object>)JsonConvertType
                .GetMethod(nameof(JsonConvert.DeserializeObject), new[] { typeof(string), typeof(Type) })
                .CreateDelegate(typeof(Func<string, Type, object>));

            internal static T Deserialize<T>(string json) => (T)DeserializeObject(json, typeof(T));
        }
    }
}
