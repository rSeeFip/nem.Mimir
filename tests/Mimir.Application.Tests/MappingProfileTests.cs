using AutoMapper;
using Mimir.Application.Common.Mappings;
using Shouldly;

namespace Mimir.Application.Tests;

public sealed class MappingProfileTests
{
    private readonly MapperConfiguration _configuration;
    private readonly IMapper _mapper;

    public MappingProfileTests()
    {
        _configuration = new MapperConfiguration(cfg =>
            cfg.AddProfile<MappingProfile>());

        _mapper = _configuration.CreateMapper();
    }

    [Fact]
    public void MappingProfile_ShouldHaveValidConfiguration()
    {
        // Assert that all mappings are properly configured
        _configuration.AssertConfigurationIsValid();
    }

    [Fact]
    public void Mapper_ShouldBeCreatable()
    {
        _mapper.ShouldNotBeNull();
    }
}
