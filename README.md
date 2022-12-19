# SpamAssassinService
 This system service controls the SpamAssassin Daemon (spamd.exe)

Compile and copy SpamAssassinService.exe and SpamAssassinService.exe.config to your "SpamAssassin for Windows" directory and run:
```SpamAssassinService.exe --install```

Example config:
```
<?xml version="1.0" encoding="utf-8" ?>
<configuration>
	<configSections>
		<section name="log4net" type="log4net.Config.Log4NetConfigurationSectionHandler, log4net" />
	</configSections>
	<appSettings>
		<add key="IsDebug" value="true"/>
		<add key="PingInterval" value="300000"/>
		<!--milliseconds = 300000 = 5 minutes-->
		<add key="PingSendTimeout" value="1000"/>
		<!--milliseconds = 1000 = 1 seconds-->
		<add key="PingReceiveTimeout" value="3000"/>
		<!--milliseconds = 3000 = 3 seconds-->
		<add key="RestartInterval" value="60"/>
		<!--60 minutes-->
		<add key="UpdateInterval" value="24"/>
		<!--24 hours-->
		<add key="Host" value="127.0.0.1"/>
		<add key="Port" value="783"/>
		<add key="RunCmd" value="C:\Program Files\SpamAssassin for Windows\spamd.exe"/>
		<add key="RunCmdArguments" value="--allow-tell --ipv4-only --syslog=&quot;C:\Program Files\SpamAssassin for Windows\Log\spamd.log&quot;"/>
		<add key="UpdateCmd" value="C:\Program Files\SpamAssassin for Windows\sa-update.exe"/>
		<add key="UpdateCmdArguments" value="-v --nogpg --channelfile &quot;C:\Program Files\SpamAssassin for Windows\UpdateChannels.txt&quot;"/>
		<add key="Priority" value="Normal"/>
		<!-- RealTime, High, AboveNormal, Normal, BelowNormal, Idle - Default = Normal-->
	</appSettings>
	<log4net debug="true">
		<appender name="RollingLogFileAppender" type="log4net.Appender.RollingFileAppender">
			<file value=".\Log\" />
			<appendToFile value="true" />
			<rollingStyle value="Date" />
			<datePattern value="yyyy-MM-dd.lo\g" />
			<staticLogFileName value="false" />
			<layout type="log4net.Layout.PatternLayout">
				<conversionPattern value="%date{yyyy-MM-dd HH:mm:ss,fff} %-5level: %message%newline" />
			</layout>
		</appender>
		<appender name="Console" type="log4net.Appender.ConsoleAppender">
			<layout type="log4net.Layout.PatternLayout">
				<conversionPattern value="%date %-5level: %message%newline" />
			</layout>
		</appender>
		<root>
			<level value="DEBUG" />
			<appender-ref ref="RollingLogFileAppender" />
			<appender-ref ref="Console" />
		</root>
	</log4net>
	<startup>
		<supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.5" />
	</startup>
	<runtime>
		<gcConcurrent enabled="false"/>
		<gcServer enabled="true"/>
	</runtime>
</configuration>
```

To uninstall run:
```SpamAssassinService.exe --uninstall```
