//Base option list
namespace EkumeEnumerations
{ 

    //Powers
    public enum PowersEnum { FlyingPower, ObjectMagnet, ScoreDuplicator, ProtectorShield, TrapsConverter, KillerShield, Jetpack };

    //Player states
    public enum PlayerStatesEnum
    {
        UsingPowerToFly, UsingPowerObjectMagnet, UsingPowerScoreDuplicator, UsingPowerTrapsConverter, UsingPowerProtectorShield, UsingPowerKillerShield,
        PlayerIsGrounded, PlayerIsMovingInXAxis, IsTheSecondJump, IsUnderWater,
        PlayerWinLevel, PlayerStartedToMove, PlayerLoseHealthPoint, PlayerLoseOneLive, PlayerLoseAllLives,
        PlayerDirectionIsLeft, PlayerDirectionIsRight,
        PlayerIsInImmunityTime, PlayerIsInConstantReductionOfLife,
        PlayerIsCrouchedDown, PlayerIsRidingAMount, PlayerInLadder, PlayerMovesInLadder,
        PlayerVelocityYPositive, PlayerVelocityYNegative,
        UsingPowerJetpack, PlayerAttackWithWeapon,
        PlayerMovesInLadderToLeft, PlayerMovesInLadderToRight, PlayerMovesInLadderToDown, PlayerMovesInLadderToUp,
        PlayerLoseLevel, PlayerReappearInSavePoint,
        LevelStart, PlayerIsUsingSpecificWeaponCategory,
        PlayerIsInParkourWall //New state added in version 1.3.5
    }

    //Enemy states
    public enum EnemyStatesEnum
    {
        EnemyIsGrounded, EnemyIsMoving,
        EnemyLoseHealthPoint, EnemyDie,
        EnemyDirectionIsRight, EnemyDirectionIsLeft,
        EnemyAttack
    }

    public enum DirectionsXAxisEnum
    {
        Left, Right
    }

    public enum BooleanType
    {
        True, False
    }
}