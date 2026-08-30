using System;
using System.Collections.Generic;
using MobusTCP.Models;
using MobusTCP.Services.Interfaces;

namespace MobusTCP.Services;

public class TrajectoryService : ITrajectoryService
{
    private readonly IKinematicsService _kinematicsService;

    public TrajectoryService(IKinematicsService kinematicsService)
    {
        _kinematicsService = kinematicsService;
    }

    public List<TrajectoryPoint> GenerateTrajectory(RobotParameters p, TrajectoryConfig config)
    {
        var points = new List<TrajectoryPoint>();
        double duration = Math.Max(0.5, config.Duration);
        double dt = Math.Max(0.005, config.TimeStep);
        int steps = (int)Math.Ceiling(duration / dt) + 1;

        if (config.ProfileType == TrajectoryProfileType.LinearCartesian)
        {
            points = GenerateCartesianTrajectory(p, config, duration, dt, steps);
        }
        else
        {
            points = GenerateJointSpaceTrajectory(p, config, duration, dt, steps);
        }

        return points;
    }

    private List<TrajectoryPoint> GenerateJointSpaceTrajectory(
        RobotParameters p, TrajectoryConfig config, double duration, double dt, int steps)
    {
        var points = new List<TrajectoryPoint>();
        double[] q0 = [config.StartQ1, config.StartQ2, config.StartQ3, config.StartQ4];
        double[] qf = [config.EndQ1, config.EndQ2, config.EndQ3, config.EndQ4];

        for (int i = 0; i < steps; i++)
        {
            double t = Math.Min(i * dt, duration);
            double tau = t / duration; // normalized time [0, 1]

            double[] q = new double[4];
            double[] qDot = new double[4];
            double[] qDdot = new double[4];

            for (int j = 0; j < 4; j++)
            {
                double delta = qf[j] - q0[j];
                switch (config.ProfileType)
                {
                    case TrajectoryProfileType.QuinticPolynomial:
                        // 5th order: s(tau) = 10*tau^3 - 15*tau^4 + 6*tau^5
                        double sQuintic = 10 * Math.Pow(tau, 3) - 15 * Math.Pow(tau, 4) + 6 * Math.Pow(tau, 5);
                        double dsQuintic = (30 * Math.Pow(tau, 2) - 60 * Math.Pow(tau, 3) + 30 * Math.Pow(tau, 4)) / duration;
                        double ddsQuintic = (60 * tau - 180 * Math.Pow(tau, 2) + 120 * Math.Pow(tau, 3)) / (duration * duration);

                        q[j] = q0[j] + delta * sQuintic;
                        qDot[j] = delta * dsQuintic;
                        qDdot[j] = delta * ddsQuintic;
                        break;

                    case TrajectoryProfileType.CubicPolynomial:
                        // 3rd order: s(tau) = 3*tau^2 - 2*tau^3
                        double sCubic = 3 * Math.Pow(tau, 2) - 2 * Math.Pow(tau, 3);
                        double dsCubic = (6 * tau - 6 * Math.Pow(tau, 2)) / duration;
                        double ddsCubic = (6 - 12 * tau) / (duration * duration);

                        q[j] = q0[j] + delta * sCubic;
                        qDot[j] = delta * dsCubic;
                        qDdot[j] = delta * ddsCubic;
                        break;

                    case TrajectoryProfileType.TrapezoidalVelocity:
                    default:
                        // LSPB with blend time tb = duration / 4
                        double tb = duration / 4.0;
                        double vFlat = delta / (duration - tb);
                        double a = vFlat / tb;

                        if (t <= tb)
                        {
                            q[j] = q0[j] + 0.5 * a * t * t;
                            qDot[j] = a * t;
                            qDdot[j] = a;
                        }
                        else if (t <= duration - tb)
                        {
                            q[j] = q0[j] + 0.5 * a * tb * tb + vFlat * (t - tb);
                            qDot[j] = vFlat;
                            qDdot[j] = 0.0;
                        }
                        else
                        {
                            double trem = duration - t;
                            q[j] = qf[j] - 0.5 * a * trem * trem;
                            qDot[j] = a * trem;
                            qDdot[j] = -a;
                        }
                        break;
                }
            }

            // Calculate Cartesian FK at this point
            var fk = _kinematicsService.ForwardKinematics(p, q[0], q[1], q[2], q[3]);

            // Calculate Dynamic Torques at this point
            var dyn = _kinematicsService.CalculateDynamics(p,
                q[0], q[1], q[2], q[3],
                qDot[0], qDot[1], qDot[2], qDot[3],
                qDdot[0], qDdot[1], qDdot[2], qDdot[3]);

            points.Add(new TrajectoryPoint
            {
                Time = t,
                Q1 = q[0],
                Q2 = q[1],
                Q3 = q[2],
                Q4 = q[3],
                Q1Dot = qDot[0],
                Q2Dot = qDot[1],
                Q3Dot = qDot[2],
                Q4Dot = qDot[3],
                Q1Ddot = qDdot[0],
                Q2Ddot = qDdot[1],
                Q3Ddot = qDdot[2],
                Q4Ddot = qDdot[3],
                X = fk.X,
                Y = fk.Y,
                Z = fk.Z,
                Pitch = fk.Pitch,
                Tau1 = dyn.Tau1,
                Tau2 = dyn.Tau2,
                Tau3 = dyn.Tau3,
                Tau4 = dyn.Tau4
            });
        }

        return points;
    }

    private List<TrajectoryPoint> GenerateCartesianTrajectory(
        RobotParameters p, TrajectoryConfig config, double duration, double dt, int steps)
    {
        var points = new List<TrajectoryPoint>();

        double x0 = config.StartX, y0 = config.StartY, z0 = config.StartZ, p0 = config.StartPitch;
        double xf = config.EndX, yf = config.EndY, zf = config.EndZ, pf = config.EndPitch;

        double prevQ1 = config.StartQ1, prevQ2 = config.StartQ2, prevQ3 = config.StartQ3, prevQ4 = config.StartQ4;
        double prevQ1Dot = 0, prevQ2Dot = 0, prevQ3Dot = 0, prevQ4Dot = 0;

        for (int i = 0; i < steps; i++)
        {
            double t = Math.Min(i * dt, duration);
            double tau = t / duration;

            // Quintic blend for smooth Cartesian motion
            double s = 10 * Math.Pow(tau, 3) - 15 * Math.Pow(tau, 4) + 6 * Math.Pow(tau, 5);

            double curX = x0 + (xf - x0) * s;
            double curY = y0 + (yf - y0) * s;
            double curZ = z0 + (zf - z0) * s;
            double curPitch = p0 + (pf - p0) * s;

            var ik = _kinematicsService.InverseKinematics(p, curX, curY, curZ, curPitch, config.ElbowUp);

            double q1 = ik.IsReachable ? ik.Q1 : prevQ1;
            double q2 = ik.IsReachable ? ik.Q2 : prevQ2;
            double q3 = ik.IsReachable ? ik.Q3 : prevQ3;
            double q4 = ik.IsReachable ? ik.Q4 : prevQ4;

            // Numerical differentiation for velocity and acceleration
            double q1Dot = i > 0 ? (q1 - prevQ1) / dt : 0.0;
            double q2Dot = i > 0 ? (q2 - prevQ2) / dt : 0.0;
            double q3Dot = i > 0 ? (q3 - prevQ3) / dt : 0.0;
            double q4Dot = i > 0 ? (q4 - prevQ4) / dt : 0.0;

            double q1Ddot = i > 0 ? (q1Dot - prevQ1Dot) / dt : 0.0;
            double q2Ddot = i > 0 ? (q2Dot - prevQ2Dot) / dt : 0.0;
            double q3Ddot = i > 0 ? (q3Dot - prevQ3Dot) / dt : 0.0;
            double q4Ddot = i > 0 ? (q4Dot - prevQ4Dot) / dt : 0.0;

            prevQ1 = q1; prevQ2 = q2; prevQ3 = q3; prevQ4 = q4;
            prevQ1Dot = q1Dot; prevQ2Dot = q2Dot; prevQ3Dot = q3Dot; prevQ4Dot = q4Dot;

            var dyn = _kinematicsService.CalculateDynamics(p,
                q1, q2, q3, q4,
                q1Dot, q2Dot, q3Dot, q4Dot,
                q1Ddot, q2Ddot, q3Ddot, q4Ddot);

            points.Add(new TrajectoryPoint
            {
                Time = t,
                Q1 = q1,
                Q2 = q2,
                Q3 = q3,
                Q4 = q4,
                Q1Dot = q1Dot,
                Q2Dot = q2Dot,
                Q3Dot = q3Dot,
                Q4Dot = q4Dot,
                Q1Ddot = q1Ddot,
                Q2Ddot = q2Ddot,
                Q3Ddot = q3Ddot,
                Q4Ddot = q4Ddot,
                X = curX,
                Y = curY,
                Z = curZ,
                Pitch = curPitch,
                Tau1 = dyn.Tau1,
                Tau2 = dyn.Tau2,
                Tau3 = dyn.Tau3,
                Tau4 = dyn.Tau4
            });
        }

        return points;
    }
}
