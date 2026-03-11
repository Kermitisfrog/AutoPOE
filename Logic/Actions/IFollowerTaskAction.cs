using AutoPOE.Logic.Sequences;

namespace AutoPOE.Logic.Actions
{
    public interface IFollowerTaskAction
    {
        void Execute(FollowerActionContext context, TaskNode task);
    }
}
