/**
 * This file is part of AriaNet by huming2207, licensed under the CC-BY-NC-SA 3.0 Australian Licence.
 * You can find the original code in this GitHub repository: https://github.com/huming2207/AriaNet
 */

using System.Text.Json.Serialization;

namespace AriaNet.Attributes
{
    public class AriaOption
    {
        [JsonPropertyName("all-proxy")]
        public string AllProxy { get; set; }

        [JsonPropertyName("all-proxy-passwd")]
        public string AllProxyPasswd { get; set; }

        [JsonPropertyName("all-proxy-user")]
        public string AllProxyUser { get; set; }

        [JsonPropertyName("allow-overwrite")]
        public string AllowOverwrite { get; set; }

        [JsonPropertyName("allow-piece-length-change")]
        public string AllowPieceLengthChange { get; set; }

        [JsonPropertyName("always-resume")]
        public string AlwaysResume { get; set; }

        [JsonPropertyName("async-dns")]
        public string AsyncDns { get; set; }

        [JsonPropertyName("auto-file-renaming")]
        public string AutoFileRenaming { get; set; }

        [JsonPropertyName("bt-enable-hook-after-hash-check")]
        public string BtEnableHookAfterHashCheck { get; set; }

        [JsonPropertyName("bt-enable-lpd")]
        public string BtEnableLpd { get; set; }

        [JsonPropertyName("bt-exclude-tracker")]
        public string BtExcludeTracker { get; set; }

        [JsonPropertyName("bt-external-ip")]
        public string BtExternalIp { get; set; }

        [JsonPropertyName("bt-force-encryption")]
        public string BtForceEncryption { get; set; }

        [JsonPropertyName("bt-hash-check-seed")]
        public string BtHashCheckSeed { get; set; }

        [JsonPropertyName("bt-max-peers")]
        public string BtMaxPeers { get; set; }

        [JsonPropertyName("bt-metadata-only")]
        public string BtMetadataOnly { get; set; }

        [JsonPropertyName("bt-min-crypto-level")]
        public string BtMinCryptoLevel { get; set; }

        [JsonPropertyName("bt-prioritize-piece")]
        public string BtPrioritizePiece { get; set; }

        [JsonPropertyName("bt-remove-unselected-file")]
        public string BtRemoveUnselectedFile { get; set; }

        [JsonPropertyName("bt-request-peer-speed-limit")]
        public string BtRequestPeerSpeedLimit { get; set; }

        [JsonPropertyName("bt-require-crypto")]
        public string BtRequireCrypto { get; set; }

        [JsonPropertyName("bt-save-metadata")]
        public string BtSaveMetadata { get; set; }

        [JsonPropertyName("bt-seed-unverified")]
        public string BtSeedUnverified { get; set; }

        [JsonPropertyName("bt-stop-timeout")]
        public string BtStopTimeout { get; set; }

        [JsonPropertyName("bt-tracker")]
        public string BtTracker { get; set; }

        [JsonPropertyName("bt-tracker-connect-timeout")]
        public string BtTrackerConnectTimeout { get; set; }

        [JsonPropertyName("bt-tracker-interval")]
        public string BtTrackerInterval { get; set; }

        [JsonPropertyName("bt-tracker-timeout")]
        public string BtTrackerTimeout { get; set; }

        [JsonPropertyName("check-integrity")]
        public string CheckIntegrity { get; set; }

        [JsonPropertyName("checksum")]
        public string Checksum { get; set; }

        [JsonPropertyName("conditional-get")]
        public string ConditionalGet { get; set; }

        [JsonPropertyName("connect-timeout")]
        public string ConnectTimeout { get; set; }

        [JsonPropertyName("content-disposition-default-utf8")]
        public string ContentDispositionDefaultUtf8 { get; set; }

        [JsonPropertyName("continue")]
        public string Continue { get; set; }

        [JsonPropertyName("dir")]
        public string Dir { get; set; }

        [JsonPropertyName("dry-run")]
        public string DryRun { get; set; }

        [JsonPropertyName("enable-http-keep-alive")]
        public string EnableHttpKeepAlive { get; set; }

        [JsonPropertyName("enable-http-pipelining")]
        public string EnableHttpPipelining { get; set; }

        [JsonPropertyName("enable-mmap")]
        public string EnableMmap { get; set; }

        [JsonPropertyName("enable-peer-exchange")]
        public string EnablePeerExchange { get; set; }

        [JsonPropertyName("file-allocation")]
        public string FileAllocation { get; set; }

        [JsonPropertyName("follow-metalink")]
        public string FollowMetalink { get; set; }

        [JsonPropertyName("follow-torrent")]
        public string FollowTorrent { get; set; }

        [JsonPropertyName("force-save")]
        public string ForceSave { get; set; }

        [JsonPropertyName("ftp-passwd")]
        public string FtpPasswd { get; set; }

        [JsonPropertyName("ftp-pasv")]
        public string FtpPasv { get; set; }

        [JsonPropertyName("ftp-proxy")]
        public string FtpProxy { get; set; }

        [JsonPropertyName("ftp-proxy-passwd")]
        public string FtpProxyPasswd { get; set; }

        [JsonPropertyName("ftp-proxy-user")]
        public string FtpProxyUser { get; set; }

        [JsonPropertyName("ftp-reuse-connection")]
        public string FtpReuseConnection { get; set; }

        [JsonPropertyName("ftp-type")]
        public string FtpType { get; set; }

        [JsonPropertyName("ftp-user")]
        public string FtpUser { get; set; }

        [JsonPropertyName("gid")]
        public string Gid { get; set; }

        [JsonPropertyName("hash-check-only")]
        public string HashCheckOnly { get; set; }

        [JsonPropertyName("header")]
        public string Header { get; set; }

        [JsonPropertyName("http-accept-gzip")]
        public string HttpAcceptGzip { get; set; }

        [JsonPropertyName("http-auth-challenge")]
        public string HttpAuthChallenge { get; set; }

        [JsonPropertyName("http-no-cache")]
        public string HttpNoCache { get; set; }

        [JsonPropertyName("http-passwd")]
        public string HttpPasswd { get; set; }

        [JsonPropertyName("http-proxy")]
        public string HttpProxy { get; set; }

        [JsonPropertyName("http-proxy-passwd")]
        public string HttpProxyPasswd { get; set; }

        [JsonPropertyName("http-proxy-user")]
        public string HttpProxyUser { get; set; }

        [JsonPropertyName("http-user")]
        public string HttpUser { get; set; }

        [JsonPropertyName("https-proxy")]
        public string HttpsProxy { get; set; }

        [JsonPropertyName("https-proxy-passwd")]
        public string HttpsProxyPasswd { get; set; }

        [JsonPropertyName("https-proxy-user")]
        public string HttpsProxyUser { get; set; }

        [JsonPropertyName("index-out")]
        public string IndexOut { get; set; }

        [JsonPropertyName("lowest-speed-limit")]
        public string LowestSpeedLimit { get; set; }

        [JsonPropertyName("max-connection-per-server")]
        public string MaxConnectionPerServer { get; set; }

        [JsonPropertyName("max-download-limit")]
        public string MaxDownloadLimit { get; set; }

        [JsonPropertyName("max-file-not-found")]
        public string MaxFileNotFound { get; set; }

        [JsonPropertyName("max-mmap-limit")]
        public string MaxMmapLimit { get; set; }

        [JsonPropertyName("max-resume-failure-tries")]
        public string MaxResumeFailureTries { get; set; }

        [JsonPropertyName("max-tries")]
        public string MaxTries { get; set; }

        [JsonPropertyName("max-upload-limit")]
        public string MaxUploadLimit { get; set; }

        [JsonPropertyName("metalink-base-uri")]
        public string MetalinkBaseUri { get; set; }

        [JsonPropertyName("metalink-enable-unique-protocol")]
        public string MetalinkEnableUniqueProtocol { get; set; }

        [JsonPropertyName("metalink-language")]
        public string MetalinkLanguage { get; set; }

        [JsonPropertyName("metalink-location")]
        public string MetalinkLocation { get; set; }

        [JsonPropertyName("metalink-os")]
        public string MetalinkOs { get; set; }

        [JsonPropertyName("metalink-preferred-protocol")]
        public string MetalinkPreferredProtocol { get; set; }

        [JsonPropertyName("metalink-version")]
        public string MetalinkVersion { get; set; }

        [JsonPropertyName("min-split-size")]
        public string MinSplitSize { get; set; }

        [JsonPropertyName("no-file-allocation-limit")]
        public string NoFileAllocationLimit { get; set; }

        [JsonPropertyName("no-netrc")]
        public string NoNetrc { get; set; }

        [JsonPropertyName("no-proxy")]
        public string NoProxy { get; set; }

        [JsonPropertyName("out")]
        public string Out { get; set; }

        [JsonPropertyName("parameterized-uri")]
        public string ParameterizedUri { get; set; }

        [JsonPropertyName("pause")]
        public string Pause { get; set; }

        [JsonPropertyName("pause-metadata")]
        public string PauseMetadata { get; set; }

        [JsonPropertyName("piece-length")]
        public string PieceLength { get; set; }

        [JsonPropertyName("proxy-method")]
        public string ProxyMethod { get; set; }

        [JsonPropertyName("realtime-chunk-checksum")]
        public string RealtimeChunkChecksum { get; set; }

        [JsonPropertyName("referer")]
        public string Referer { get; set; }

        [JsonPropertyName("remote-time")]
        public string RemoteTime { get; set; }

        [JsonPropertyName("remove-control-file")]
        public string RemoveControlFile { get; set; }

        [JsonPropertyName("retry-wait")]
        public string RetryWait { get; set; }

        [JsonPropertyName("reuse-uri")]
        public string ReuseUri { get; set; }

        [JsonPropertyName("rpc-save-upload-metadata")]
        public string RpcSaveUploadMetadata { get; set; }

        [JsonPropertyName("seed-ratio")]
        public string SeedRatio { get; set; }

        [JsonPropertyName("seed-time")]
        public string SeedTime { get; set; }

        [JsonPropertyName("select-file")]
        public string SelectFile { get; set; }

        [JsonPropertyName("split")]
        public string Split { get; set; }

        [JsonPropertyName("ssh-host-key-md")]
        public string SshHostKeyMd { get; set; }

        [JsonPropertyName("stream-piece-selector")]
        public string StreamPieceSelector { get; set; }

        [JsonPropertyName("timeout")]
        public string Timeout { get; set; }

        [JsonPropertyName("uri-selector")]
        public string UriSelector { get; set; }

        [JsonPropertyName("use-head")]
        public string UseHead { get; set; }

        [JsonPropertyName("user-agent")]
        public string UserAgent { get; set; }
    }
}