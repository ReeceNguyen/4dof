using System.Collections.Generic;
using SCADA.Models;

namespace SCADA.Services.Interfaces;

public interface ITrajectoryService
{
    List<TrajectoryPoint> GenerateTrajectory(RobotParameters p, TrajectoryConfig config);
}
