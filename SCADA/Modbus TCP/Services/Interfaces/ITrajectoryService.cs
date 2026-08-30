using System.Collections.Generic;
using MobusTCP.Models;

namespace MobusTCP.Services.Interfaces;

public interface ITrajectoryService
{
    List<TrajectoryPoint> GenerateTrajectory(RobotParameters p, TrajectoryConfig config);
}
