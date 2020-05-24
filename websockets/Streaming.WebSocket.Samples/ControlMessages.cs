namespace Streaming.WebSocket.Samples
{
	public class HeartbeatControlMessage
	{
		public string ReferenceId { get; set; }
		public Heartbeat[] Heartbeats { get; set; }
	}

	public class Heartbeat
	{
		public string OriginatingReferenceId { get; set; }
		public string Reason { get; set; }
	}

	public class ResetSubscriptionsControlMessage
	{
		public string ReferenceId { get; set; }
		public string[] TargetReferenceIds { get; set; }
	}

	public class DisconnectControlMessage
	{
		public string ReferenceId { get; set; }
	}
}
