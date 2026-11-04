namespace LeagueOfStats.Application;

public sealed class GameUnavailableException(Exception innerException)
    : Exception("La API local no está disponible; esperando partida.", innerException);
