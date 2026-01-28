using System;

namespace VARCOVoice
{
    /// <summary>
    /// Base exception for VARCO Voice errors
    /// </summary>
    public class VarcoException : Exception
    {
        public int StatusCode { get; }
        public string RequestId { get; }
        
        public VarcoException(string message) : base(message) { }
        
        public VarcoException(string message, int statusCode, string requestId = null) 
            : base(message)
        {
            StatusCode = statusCode;
            RequestId = requestId;
        }
        
        public VarcoException(string message, Exception innerException) 
            : base(message, innerException) { }
    }
    
    /// <summary>
    /// API Key is missing or invalid
    /// </summary>
    public class VarcoAuthException : VarcoException
    {
        public VarcoAuthException() 
            : base("API Key is missing or unauthorized. Please check your VARCO Voice configuration.", 401) { }
        
        public VarcoAuthException(string requestId) 
            : base("API Key is missing or unauthorized. Please check your VARCO Voice configuration.", 401, requestId) { }
    }
    
    /// <summary>
    /// Bad request (invalid parameters)
    /// </summary>
    public class VarcoBadRequestException : VarcoException
    {
        public VarcoBadRequestException(string details) 
            : base($"Bad request: {details}", 400) { }
    }
    
    /// <summary>
    /// Rate limit exceeded
    /// </summary>
    public class VarcoRateLimitException : VarcoException
    {
        public int RetryAfterSeconds { get; }
        
        public VarcoRateLimitException(int retryAfter = 60) 
            : base($"Rate limit exceeded. Please retry after {retryAfter} seconds.", 429)
        {
            RetryAfterSeconds = retryAfter;
        }
    }
    
    /// <summary>
    /// Voice not found
    /// </summary>
    public class VarcoVoiceNotFoundException : VarcoException
    {
        public string VoiceName { get; }
        
        public VarcoVoiceNotFoundException(string voiceName) 
            : base($"Voice '{voiceName}' not found. Please check the voice name.", 400)
        {
            VoiceName = voiceName;
        }
    }
    
    /// <summary>
    /// Text too long exception
    /// </summary>
    public class VarcoTextTooLongException : VarcoException
    {
        public int MaxBytes { get; } = 1200;
        public int ActualBytes { get; }
        
        public VarcoTextTooLongException(int actualBytes) 
            : base($"Text is too long. Maximum is 1,200 bytes (UTF-8), but got {actualBytes} bytes.", 400)
        {
            ActualBytes = actualBytes;
        }
    }
    
    /// <summary>
    /// Server error
    /// </summary>
    public class VarcoServerException : VarcoException
    {
        public VarcoServerException(string message = "Internal server error. Please try again later.") 
            : base(message, 500) { }
    }
    
    /// <summary>
    /// Network connectivity error
    /// </summary>
    public class VarcoNetworkException : VarcoException
    {
        public VarcoNetworkException(string message, Exception innerException = null) 
            : base($"Network error: {message}", innerException) { }
    }
}
