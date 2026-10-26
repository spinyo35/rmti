namespace Rtmi.Api;

public interface IMatchControl
{
    string Status { get; }
    void Start(int speed);
    void Pause();
    void Reset();
    void Demo();
}
