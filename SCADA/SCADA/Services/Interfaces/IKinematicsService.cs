using System.Collections.Generic;
using SCADA.Models;

namespace SCADA.Services.Interfaces;

public interface IKinematicsService
{
    List<DhParameterRow> CalculateDhTable(RobotParameters p, double q1Deg, double q2Deg, double q3Deg, double q4Deg);

    (double X, double Y, double Z, double Pitch, double Yaw, double Roll,
     (double X, double Y, double Z) P0,
     (double X, double Y, double Z) P1,
     (double X, double Y, double Z) P2,
     (double X, double Y, double Z) P3,
     (double X, double Y, double Z) P4)
    ForwardKinematics(RobotParameters p, double q1Deg, double q2Deg, double q3Deg, double q4Deg);

    (bool IsReachable, double Q1, double Q2, double Q3, double Q4, string Message)
    InverseKinematics(RobotParameters p, double x, double y, double z, double pitchDeg, bool elbowUp);

    (double[,] J, double Determinant, double Manipulability, bool IsSingular)
    CalculateJacobian(RobotParameters p, double q1Deg, double q2Deg, double q3Deg, double q4Deg);

    (double Tau1, double Tau2, double Tau3, double Tau4, double TotalPower)
    CalculateDynamics(RobotParameters p,
                      double q1Deg, double q2Deg, double q3Deg, double q4Deg,
                      double q1DotDeg, double q2DotDeg, double q3DotDeg, double q4DotDeg,
                      double q1DdotDeg, double q2DdotDeg, double q3DdotDeg, double q4DdotDeg);
}
