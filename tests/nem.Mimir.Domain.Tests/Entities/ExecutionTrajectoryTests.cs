using nem.Mimir.Domain.Entities;
using nem.Mimir.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace nem.Mimir.Domain.Tests.Entities;

public class ExecutionTrajectoryTests
{
    [Fact]
    public void Create_With_Valid_Parameters_Should_Create_ExecutionTrajectory()
    {
        // Arrange
        var sessionId = "session-1";
        var agentId = "agent-1";

        // Act
        var trajectory = ExecutionTrajectory.Create(sessionId, agentId);

        // Assert
        trajectory.Id.ShouldNotBe(default);
        trajectory.SessionId.ShouldBe(sessionId);
        trajectory.AgentId.ShouldBe(agentId);
        trajectory.StartedAt.ShouldNotBe(default);
        trajectory.CompletedAt.ShouldBeNull();
        trajectory.IsSuccess.ShouldBeFalse();
        trajectory.ErrorMessage.ShouldBeNull();
        trajectory.TotalSteps.ShouldBe(0);
        trajectory.Steps.Count.ShouldBe(0);
    }

    [Fact]
    public void Create_With_Empty_SessionId_Should_Throw_ArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => ExecutionTrajectory.Create(string.Empty, "agent-1"));
    }

    [Fact]
    public void Create_With_Empty_AgentId_Should_Throw_ArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => ExecutionTrajectory.Create("session-1", string.Empty));
    }

    [Fact]
    public void AddStep_Should_Append_Step_And_Increment_Count()
    {
        // Arrange
        var trajectory = ExecutionTrajectory.Create("session-1", "agent-1");
        var step = TrajectoryStep.Create("Dispatch", "input", "output", TimeSpan.FromMilliseconds(10), true);

        // Act
        trajectory.AddStep(step);

        // Assert
        trajectory.Steps.Count.ShouldBe(1);
        trajectory.TotalSteps.ShouldBe(1);
        trajectory.Steps.Single().Action.ShouldBe("Dispatch");
    }

    [Fact]
    public void Complete_Should_Set_CompletedAt_And_IsSuccess()
    {
        // Arrange
        var trajectory = ExecutionTrajectory.Create("session-1", "agent-1");

        // Act
        trajectory.Complete();

        // Assert
        trajectory.CompletedAt.ShouldNotBeNull();
        trajectory.IsSuccess.ShouldBeTrue();
        trajectory.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void Fail_Should_Set_CompletedAt_And_ErrorMessage()
    {
        // Arrange
        var trajectory = ExecutionTrajectory.Create("session-1", "agent-1");

        // Act
        trajectory.Fail("boom");

        // Assert
        trajectory.CompletedAt.ShouldNotBeNull();
        trajectory.IsSuccess.ShouldBeFalse();
        trajectory.ErrorMessage.ShouldBe("boom");
    }

    [Fact]
    public void Complete_Already_Completed_Should_Throw_InvalidOperationException()
    {
        // Arrange
        var trajectory = ExecutionTrajectory.Create("session-1", "agent-1");
        trajectory.Complete();

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => trajectory.Complete());
    }

    [Fact]
    public void AddStep_To_Completed_Trajectory_Should_Throw_InvalidOperationException()
    {
        // Arrange
        var trajectory = ExecutionTrajectory.Create("session-1", "agent-1");
        trajectory.Complete();
        var step = TrajectoryStep.Create("Dispatch", "in", "out", TimeSpan.FromMilliseconds(10), true);

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => trajectory.AddStep(step));
    }
}
