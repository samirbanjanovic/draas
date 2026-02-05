using DRaaS.CoreLib.Models;
using DRaaS.CoreLib.Services;
using DRaaS.WebApi.Models.Common;
using DRaaS.WebApi.Models.Requests;
using DRaaS.WebApi.Models.Responses;

namespace DRaaS.WebApi.Models.Mappers;

/// <summary>
/// Bidirectional mapper between domain models and DTOs.
/// Handles array ↔ dictionary transformations for patch operations.
/// </summary>
public static class InstanceMapper
{
    #region Domain → Response DTO

    /// <summary>
    /// Maps a domain instance to a response DTO.
    /// </summary>
    public static InstanceResponseDto ToResponseDto(this DrasiInstance instance)
    {
        return new InstanceResponseDto
        {
            InstanceId = instance.InstanceId,
            Name = instance.Name,
            Description = instance.Description,
            Owners = instance.Owners,
            CreatedAt = instance.CreatedAt,
            LastUpdatedAt = instance.LastUpdatedAt,
            Status = instance.Status.ToString(),
            RuntimeStatus = instance.RuntimeStatus?.ToString(),
            Configuration = instance.Configuration.ToResponseDto(),
            Metadata = instance.MetaData,
            Placement = instance.Placement?.ToResponseDto(),
            IsFullyOperational = instance.IsFullyOperational,
            IsReadyForDeployment = instance.IsReadyForDeployment,
            NeedsCleanup = instance.NeedsCleanup
        };
    }

    public static ConfigurationResponseDto ToResponseDto(this DrasiConfiguration config)
    {
        return new ConfigurationResponseDto
        {
            Host = config.Host,
            Port = config.Port,
            LogLevel = config.LogLevel,
            Sources = config.Sources?.Select(s => s.ToDto()).ToList(),
            Queries = config.Queries?.Select(q => q.ToDto()).ToList(),
            Reactions = config.Reactions?.Select(r => r.ToDto()).ToList()
        };
    }

    public static PlacementResponseDto ToResponseDto(this PlacementProviderRuntimeInfo placement)
    {
        return new PlacementResponseDto
        {
            InstanceId = placement.InstanceId,
            PlatformType = placement.PlatformType,
            Status = placement.Status.ToString(),
            DeployedAt = placement.DeployedAt,
            StartedAt = placement.StartedAt,
            StoppedAt = placement.StoppedAt,
            LastSyncedAt = placement.LastSyncedAt,
            PlatformMetadata = placement.PlatformMetadata
        };
    }

    public static PlatformDetailsDto ToResponseDto(this PlatformDetails platform)
    {
        return new PlatformDetailsDto
        {
            PlatformType = platform.PlatformType,
            IsAvailable = platform.IsAvailable,
            IsDefault = platform.IsDefault,
            InstanceCount = platform.InstanceCount,
            Labels = platform.Labels,
            Metadata = platform.Metadata
        };
    }

    #endregion

    #region Domain → Patch DTO (arrays → dictionaries)

    /// <summary>
    /// Maps a domain instance to a patch DTO with dictionary-based collections.
    /// Used before applying JSON Patch operations.
    /// </summary>
    public static InstancePatchDto ToPatchDto(this DrasiInstance instance)
    {
        return new InstancePatchDto
        {
            Name = instance.Name,
            Description = instance.Description,
            Owners = instance.Owners,
            Configuration = instance.Configuration.ToPatchDto(),
            Metadata = instance.MetaData
        };
    }

    public static ConfigurationPatchDto ToPatchDto(this DrasiConfiguration config)
    {
        return new ConfigurationPatchDto
        {
            Host = config.Host,
            Port = config.Port,
            LogLevel = config.LogLevel,
            Sources = config.Sources?.ToDictionary(s => s.Id, s => s.ToDto()),
            Queries = config.Queries?.ToDictionary(q => q.Id, q => q.ToDto()),
            Reactions = config.Reactions?.ToDictionary(r => r.Id, r => r.ToDto())
        };
    }

    #endregion

    #region Request/Patch DTO → Domain

    /// <summary>
    /// Maps a create request configuration to domain model.
    /// </summary>
    public static DrasiConfiguration ToDomainModel(this ConfigurationDto dto)
    {
        return new DrasiConfiguration
        {
            Host = dto.Host,
            Port = dto.Port,
            LogLevel = dto.LogLevel,
            Sources = dto.Sources?.Select(s => s.ToDomainModel()).ToList(),
            Queries = dto.Queries?.Select(q => q.ToDomainModel()).ToList(),
            Reactions = dto.Reactions?.Select(r => r.ToDomainModel()).ToList()
        };
    }

    /// <summary>
    /// Maps a patch DTO configuration (dictionaries) to domain model (arrays).
    /// </summary>
    public static DrasiConfiguration ToDomainModel(this ConfigurationPatchDto dto)
    {
        return new DrasiConfiguration
        {
            Host = dto.Host ?? "0.0.0.0",
            Port = dto.Port ?? 8080,
            LogLevel = dto.LogLevel ?? "Info",
            Sources = dto.Sources?.Values.Select(s => s.ToDomainModel()).ToList(),
            Queries = dto.Queries?.Values.Select(q => q.ToDomainModel()).ToList(),
            Reactions = dto.Reactions?.Values.Select(r => r.ToDomainModel()).ToList()
        };
    }

    /// <summary>
    /// Applies patch DTO changes to a domain instance.
    /// Called after JSON Patch operations are applied to the DTO.
    /// </summary>
    public static void ApplyPatchDto(this DrasiInstance instance, InstancePatchDto dto)
    {
        if (dto.Name != null) instance.Name = dto.Name;
        if (dto.Description != null) instance.Description = dto.Description;
        if (dto.Owners != null) instance.Owners = dto.Owners;
        if (dto.Metadata != null) instance.MetaData = dto.Metadata;

        if (dto.Configuration != null)
        {
            instance.Configuration = dto.Configuration.ToDomainModel();
        }
    }

    #endregion

    #region Common DTO ↔ Domain Model

    public static SourceDto ToDto(this Source source)
    {
        return new SourceDto
        {
            Kind = source.Kind,
            Id = source.Id,
            AutoStart = source.AutoStart
        };
    }

    public static Source ToDomainModel(this SourceDto dto)
    {
        return new Source
        {
            Kind = dto.Kind,
            Id = dto.Id,
            AutoStart = dto.AutoStart
        };
    }

    public static QueryDto ToDto(this Query query)
    {
        return new QueryDto
        {
            Id = query.Id,
            QueryText = query.QueryText,
            Sources = query.Sources?.Select(s => new QuerySourceDto { SourceId = s.SourceId }).ToList()
        };
    }

    public static Query ToDomainModel(this QueryDto dto)
    {
        return new Query
        {
            Id = dto.Id,
            QueryText = dto.QueryText,
            Sources = dto.Sources?.Select(s => new QuerySource { SourceId = s.SourceId }).ToList()
        };
    }

    public static ReactionDto ToDto(this Reaction reaction)
    {
        return new ReactionDto
        {
            Kind = reaction.Kind,
            Id = reaction.Id,
            Queries = reaction.Queries
        };
    }

    public static Reaction ToDomainModel(this ReactionDto dto)
    {
        return new Reaction
        {
            Kind = dto.Kind,
            Id = dto.Id,
            Queries = dto.Queries
        };
    }

    #endregion
}
