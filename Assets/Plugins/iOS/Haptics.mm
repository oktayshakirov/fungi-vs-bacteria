// Native side of Utils/Haptics.cs. UIFeedbackGenerator is the only way to get
// the light taptic clicks on iOS; Unity's Handheld.Vibrate is the heavy system
// buzz and cannot express these.
//
// The generators are kept alive rather than created per call: iOS warms the
// Taptic Engine when a generator is allocated, and a freshly created one often
// drops its first tap.

#import <UIKit/UIKit.h>

static UISelectionFeedbackGenerator *selectionGenerator = nil;
static UIImpactFeedbackGenerator *lightGenerator = nil;
static UIImpactFeedbackGenerator *mediumGenerator = nil;
static UIImpactFeedbackGenerator *heavyGenerator = nil;
static UINotificationFeedbackGenerator *notificationGenerator = nil;

static void FvbEnsureGenerators(void)
{
    if (selectionGenerator != nil) return;
    selectionGenerator = [[UISelectionFeedbackGenerator alloc] init];
    lightGenerator = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
    mediumGenerator = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium];
    heavyGenerator = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
    notificationGenerator = [[UINotificationFeedbackGenerator alloc] init];
    [selectionGenerator prepare];
    [lightGenerator prepare];
}

extern "C" {

void _fvbHapticSelection(void)
{
    FvbEnsureGenerators();
    [selectionGenerator selectionChanged];
    [selectionGenerator prepare];
}

// 0 = light, 1 = medium, 2 = heavy
void _fvbHapticImpact(int style)
{
    FvbEnsureGenerators();
    UIImpactFeedbackGenerator *generator =
        style >= 2 ? heavyGenerator : (style == 1 ? mediumGenerator : lightGenerator);
    [generator impactOccurred];
    [generator prepare];
}

// 0 = success, 1 = warning, 2 = error
void _fvbHapticNotification(int type)
{
    FvbEnsureGenerators();
    UINotificationFeedbackType feedback =
        type == 0 ? UINotificationFeedbackTypeSuccess
                  : (type == 1 ? UINotificationFeedbackTypeWarning
                               : UINotificationFeedbackTypeError);
    [notificationGenerator notificationOccurred:feedback];
}

}
