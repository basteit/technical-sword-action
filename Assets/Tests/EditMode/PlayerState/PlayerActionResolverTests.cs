using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace TechnicalSwordAction.PlayerState.Tests
{
    [TestFixture]
    public sealed class PlayerActionResolverTests
    {
        private static readonly PlayerActionRequest[] Priority =
        {
            PlayerActionRequest.Dash,
            PlayerActionRequest.Parry,
            PlayerActionRequest.Special,
            PlayerActionRequest.Attack,
            PlayerActionRequest.Heal,
            PlayerActionRequest.Jump,
            PlayerActionRequest.Interact
        };

        private static IEnumerable<TestCaseData> EveryGameplayRequestMask()
        {
            for (int value = 1; value <= (int)PlayerActionRequest.Gameplay; value++)
            {
                PlayerActionRequest requests = (PlayerActionRequest)value;
                yield return new TestCaseData(requests, HighestPriorityIn(requests))
                    .SetName($"NeutralMask_{value:D3}_{requests}");
            }
        }

        private static IEnumerable<TestCaseData> LockedActionStates()
        {
            foreach (PlayerActionState state in Enum.GetValues(typeof(PlayerActionState)))
            {
                if (state != PlayerActionState.Neutral && state != PlayerActionState.Attack)
                {
                    yield return new TestCaseData(state);
                }
            }
        }

        [TestCaseSource(nameof(EveryGameplayRequestMask))]
        public void Resolve_NeutralForEveryNonEmptyGameplayMask_SelectsExactlyHighestPriority(
            PlayerActionRequest requests,
            PlayerActionRequest expected)
        {
            PlayerActionDecision decision = Resolve(
                PlayerActionState.Neutral,
                requests,
                PlayerActionRequest.Gameplay);

            Assert.That(decision.SelectedRequest, Is.EqualTo(expected));
            Assert.That(IsSingleBit(decision.SelectedRequest), Is.True);
            Assert.That(decision.LowerPriorityRequests,
                Is.EqualTo(requests & ~expected));
            Assert.That(decision.UnavailableRequests, Is.EqualTo(PlayerActionRequest.None));
            Assert.That(decision.StateRejectedRequests, Is.EqualTo(PlayerActionRequest.None));
        }

        [Test]
        public void Resolve_AttackAndHealTogether_StartsAttackOnlyOnce()
        {
            PlayerActionRequest requests = PlayerActionRequest.Attack | PlayerActionRequest.Heal;

            PlayerActionDecision decision = Resolve(
                PlayerActionState.Neutral,
                requests,
                requests);

            Assert.That(decision.SelectedRequest, Is.EqualTo(PlayerActionRequest.Attack));
            Assert.That(decision.NextAction, Is.EqualTo(PlayerActionState.Attack));
            Assert.That(decision.LowerPriorityRequests, Is.EqualTo(PlayerActionRequest.Heal));
            Assert.That(decision.HasSelection, Is.True);
            Assert.That(decision.HasActionTransition, Is.True);
        }

        [Test]
        public void Resolve_HigherPriorityRequestsUnavailable_FallsBackToFirstLegalRequest()
        {
            PlayerActionRequest requests = PlayerActionRequest.Gameplay;
            PlayerActionRequest legal = PlayerActionRequest.Attack |
                PlayerActionRequest.Heal |
                PlayerActionRequest.Jump |
                PlayerActionRequest.Interact;

            PlayerActionDecision decision = Resolve(
                PlayerActionState.Neutral,
                requests,
                legal);

            Assert.That(decision.SelectedRequest, Is.EqualTo(PlayerActionRequest.Attack));
            Assert.That(decision.UnavailableRequests, Is.EqualTo(
                PlayerActionRequest.Dash |
                PlayerActionRequest.Parry |
                PlayerActionRequest.Special));
            Assert.That(decision.LowerPriorityRequests, Is.EqualTo(
                PlayerActionRequest.Heal |
                PlayerActionRequest.Jump |
                PlayerActionRequest.Interact));
            Assert.That(decision.RejectedRequests,
                Is.EqualTo(requests & ~PlayerActionRequest.Attack));
        }

        [Test]
        public void Resolve_NoRequestedAction_KeepsCurrentStateWithoutSelection()
        {
            PlayerActionDecision decision = Resolve(
                PlayerActionState.Neutral,
                PlayerActionRequest.None,
                PlayerActionRequest.Gameplay);

            Assert.That(decision.SelectedRequest, Is.EqualTo(PlayerActionRequest.None));
            Assert.That(decision.NextAction, Is.EqualTo(PlayerActionState.Neutral));
            Assert.That(decision.HasSelection, Is.False);
            Assert.That(decision.HasActionTransition, Is.False);
            Assert.That(decision.RejectedRequests, Is.EqualTo(PlayerActionRequest.None));
        }

        [Test]
        public void Resolve_PauseWithGameplayRequests_SelectsExternalPauseAndKeepsActionState()
        {
            PlayerActionRequest gameplay = PlayerActionRequest.Dash |
                PlayerActionRequest.Attack |
                PlayerActionRequest.Interact;
            PlayerActionRequest requests = PlayerActionRequest.Pause | gameplay;

            PlayerActionDecision decision = Resolve(
                PlayerActionState.Attack,
                requests,
                gameplay,
                PlayerAttackCancelWindow.All);

            Assert.That(decision.PauseRequested, Is.True);
            Assert.That(decision.SelectedRequest, Is.EqualTo(PlayerActionRequest.Pause));
            Assert.That(decision.CurrentAction, Is.EqualTo(PlayerActionState.Attack));
            Assert.That(decision.NextAction, Is.EqualTo(PlayerActionState.Attack));
            Assert.That(decision.LowerPriorityRequests, Is.EqualTo(gameplay));
            Assert.That(decision.HasActionTransition, Is.False);
            Assert.That(Enum.GetNames(typeof(PlayerActionState)),
                Does.Not.Contain("Pause"));
        }

        [Test]
        public void Resolve_PauseDoesNotRequireGameplayLegality()
        {
            PlayerActionDecision decision = Resolve(
                PlayerActionState.Hit,
                PlayerActionRequest.Pause,
                PlayerActionRequest.None);

            Assert.That(decision.SelectedRequest, Is.EqualTo(PlayerActionRequest.Pause));
            Assert.That(decision.PauseRequested, Is.True);
            Assert.That(decision.UnavailableRequests, Is.EqualTo(PlayerActionRequest.None));
        }

        [Test]
        public void Resolve_PauseWithUnavailableGameplay_StillRejectsGameplayByPausePriority()
        {
            PlayerActionDecision decision = Resolve(
                PlayerActionState.Hit,
                PlayerActionRequest.Pause | PlayerActionRequest.Special,
                PlayerActionRequest.None);

            Assert.That(decision.SelectedRequest, Is.EqualTo(PlayerActionRequest.Pause));
            Assert.That(decision.PauseRequested, Is.True);
            Assert.That(decision.UnavailableRequests, Is.EqualTo(PlayerActionRequest.Special));
            Assert.That(decision.LowerPriorityRequests, Is.EqualTo(PlayerActionRequest.Special));
        }

        [Test]
        public void Resolve_AttackWithDefenseWindow_AllowsOnlyDashAndParry()
        {
            PlayerActionDecision decision = Resolve(
                PlayerActionState.Attack,
                PlayerActionRequest.Gameplay,
                PlayerActionRequest.Gameplay,
                PlayerAttackCancelWindow.Defense);

            Assert.That(decision.SelectedRequest, Is.EqualTo(PlayerActionRequest.Dash));
            Assert.That(decision.NextAction, Is.EqualTo(PlayerActionState.Dash));
            Assert.That(decision.LowerPriorityRequests, Is.EqualTo(PlayerActionRequest.Parry));
            Assert.That(decision.StateRejectedRequests, Is.EqualTo(
                PlayerActionRequest.Special |
                PlayerActionRequest.Attack |
                PlayerActionRequest.Heal |
                PlayerActionRequest.Jump |
                PlayerActionRequest.Interact));
        }

        [Test]
        public void Resolve_AttackDefenseCancel_WhenDashUnavailable_FallsBackToParry()
        {
            PlayerActionRequest requests = PlayerActionRequest.Dash | PlayerActionRequest.Parry;

            PlayerActionDecision decision = Resolve(
                PlayerActionState.Attack,
                requests,
                PlayerActionRequest.Parry,
                PlayerAttackCancelWindow.Defense);

            Assert.That(decision.SelectedRequest, Is.EqualTo(PlayerActionRequest.Parry));
            Assert.That(decision.NextAction, Is.EqualTo(PlayerActionState.Parry));
            Assert.That(decision.UnavailableRequests, Is.EqualTo(PlayerActionRequest.Dash));
            Assert.That(decision.StateRejectedRequests, Is.EqualTo(PlayerActionRequest.None));
        }

        [Test]
        public void Resolve_AttackWithLateWindow_AllowsOnlySpecialAndJump()
        {
            PlayerActionDecision decision = Resolve(
                PlayerActionState.Attack,
                PlayerActionRequest.Gameplay,
                PlayerActionRequest.Gameplay,
                PlayerAttackCancelWindow.Late);

            Assert.That(decision.SelectedRequest, Is.EqualTo(PlayerActionRequest.Special));
            Assert.That(decision.NextAction, Is.EqualTo(PlayerActionState.Special));
            Assert.That(decision.LowerPriorityRequests, Is.EqualTo(PlayerActionRequest.Jump));
            Assert.That(decision.StateRejectedRequests, Is.EqualTo(
                PlayerActionRequest.Dash |
                PlayerActionRequest.Parry |
                PlayerActionRequest.Attack |
                PlayerActionRequest.Heal |
                PlayerActionRequest.Interact));
        }

        [Test]
        public void Resolve_AttackWithBothWindows_UsesGlobalPriorityAcrossCancelTypes()
        {
            PlayerActionRequest requests = PlayerActionRequest.Parry |
                PlayerActionRequest.Special |
                PlayerActionRequest.Jump;

            PlayerActionDecision decision = Resolve(
                PlayerActionState.Attack,
                requests,
                requests,
                PlayerAttackCancelWindow.All);

            Assert.That(decision.SelectedRequest, Is.EqualTo(PlayerActionRequest.Parry));
            Assert.That(decision.NextAction, Is.EqualTo(PlayerActionState.Parry));
            Assert.That(decision.LowerPriorityRequests,
                Is.EqualTo(PlayerActionRequest.Special | PlayerActionRequest.Jump));
        }

        [Test]
        public void Resolve_AttackWithoutCancelWindow_RejectsEveryGameplayRequestAndContinuesAttack()
        {
            PlayerActionDecision decision = Resolve(
                PlayerActionState.Attack,
                PlayerActionRequest.Gameplay,
                PlayerActionRequest.Gameplay,
                PlayerAttackCancelWindow.None);

            Assert.That(decision.SelectedRequest, Is.EqualTo(PlayerActionRequest.None));
            Assert.That(decision.NextAction, Is.EqualTo(PlayerActionState.Attack));
            Assert.That(decision.StateRejectedRequests,
                Is.EqualTo(PlayerActionRequest.Gameplay));
            Assert.That(decision.HasSelection, Is.False);
        }

        [Test]
        public void Resolve_AttackLateCancel_WhenSpecialUnavailable_FallsBackToJump()
        {
            PlayerActionRequest requests = PlayerActionRequest.Special | PlayerActionRequest.Jump;

            PlayerActionDecision decision = Resolve(
                PlayerActionState.Attack,
                requests,
                PlayerActionRequest.Jump,
                PlayerAttackCancelWindow.Late);

            Assert.That(decision.SelectedRequest, Is.EqualTo(PlayerActionRequest.Jump));
            Assert.That(decision.NextAction, Is.EqualTo(PlayerActionState.Neutral));
            Assert.That(decision.UnavailableRequests, Is.EqualTo(PlayerActionRequest.Special));
            Assert.That(decision.HasActionTransition, Is.True);
        }

        [Test]
        public void Resolve_AttackLateCancel_WhenSpecialAndJumpUnavailable_ContinuesAttack()
        {
            PlayerActionRequest requests = PlayerActionRequest.Special | PlayerActionRequest.Jump;

            PlayerActionDecision decision = Resolve(
                PlayerActionState.Attack,
                requests,
                PlayerActionRequest.None,
                PlayerAttackCancelWindow.Late);

            Assert.That(decision.SelectedRequest, Is.EqualTo(PlayerActionRequest.None));
            Assert.That(decision.NextAction, Is.EqualTo(PlayerActionState.Attack));
            Assert.That(decision.UnavailableRequests, Is.EqualTo(requests));
            Assert.That(decision.StateRejectedRequests, Is.EqualTo(PlayerActionRequest.None));
        }

        [Test]
        public void Resolve_InteractIllegalForCurrentContext_FallsBackWithoutStartingInteract()
        {
            PlayerActionRequest requests = PlayerActionRequest.Heal | PlayerActionRequest.Interact;

            PlayerActionDecision decision = Resolve(
                PlayerActionState.Neutral,
                requests,
                PlayerActionRequest.Heal);

            Assert.That(decision.SelectedRequest, Is.EqualTo(PlayerActionRequest.Heal));
            Assert.That(decision.NextAction, Is.EqualTo(PlayerActionState.Heal));
            Assert.That(decision.UnavailableRequests, Is.EqualTo(PlayerActionRequest.Interact));
        }

        [Test]
        public void Resolve_InteractAsOnlyLegalNeutralRequest_StartsInteract()
        {
            PlayerActionDecision decision = Resolve(
                PlayerActionState.Neutral,
                PlayerActionRequest.Interact,
                PlayerActionRequest.Interact);

            Assert.That(decision.SelectedRequest, Is.EqualTo(PlayerActionRequest.Interact));
            Assert.That(decision.NextAction, Is.EqualTo(PlayerActionState.Interact));
        }

        [TestCaseSource(nameof(LockedActionStates))]
        public void Resolve_NonNeutralNonAttackState_DoesNotAllowGameplayInterruption(
            PlayerActionState currentAction)
        {
            PlayerActionDecision decision = Resolve(
                currentAction,
                PlayerActionRequest.Gameplay,
                PlayerActionRequest.Gameplay,
                PlayerAttackCancelWindow.All);

            Assert.That(decision.SelectedRequest, Is.EqualTo(PlayerActionRequest.None));
            Assert.That(decision.NextAction, Is.EqualTo(currentAction));
            Assert.That(decision.StateRejectedRequests,
                Is.EqualTo(PlayerActionRequest.Gameplay));
        }

        [Test]
        public void Resolve_RepeatedOneHundredTimes_IsDeterministicForEveryNeutralAction()
        {
            foreach (PlayerActionRequest request in Priority)
            {
                PlayerActionDecision expected = Resolve(
                    PlayerActionState.Neutral,
                    request,
                    request);

                for (int iteration = 0; iteration < 100; iteration++)
                {
                    PlayerActionDecision actual = Resolve(
                        PlayerActionState.Neutral,
                        request,
                        request);

                    Assert.That(actual, Is.EqualTo(expected),
                        $"{request} changed at iteration {iteration}.");
                    Assert.That(actual.SelectedRequest, Is.EqualTo(request));
                }
            }
        }

        [Test]
        public void Resolve_UnknownFlagBits_AreIgnored()
        {
            PlayerActionRequest unknown = (PlayerActionRequest)(1 << 12);

            PlayerActionDecision decision = Resolve(
                PlayerActionState.Neutral,
                unknown | PlayerActionRequest.Attack,
                unknown | PlayerActionRequest.Attack);

            Assert.That(decision.RequestedActions, Is.EqualTo(PlayerActionRequest.Attack));
            Assert.That(decision.SelectedRequest, Is.EqualTo(PlayerActionRequest.Attack));
            Assert.That(decision.RejectedRequests, Is.EqualTo(PlayerActionRequest.None));
        }

        private static PlayerActionDecision Resolve(
            PlayerActionState currentAction,
            PlayerActionRequest requests,
            PlayerActionRequest legalRequests,
            PlayerAttackCancelWindow openCancelWindows = PlayerAttackCancelWindow.None)
        {
            return PlayerActionResolver.Resolve(
                currentAction,
                requests,
                legalRequests,
                openCancelWindows);
        }

        private static PlayerActionRequest HighestPriorityIn(PlayerActionRequest requests)
        {
            foreach (PlayerActionRequest request in Priority)
            {
                if ((requests & request) != 0)
                {
                    return request;
                }
            }

            return PlayerActionRequest.None;
        }

        private static bool IsSingleBit(PlayerActionRequest request)
        {
            int value = (int)request;
            return value != 0 && (value & (value - 1)) == 0;
        }
    }
}
