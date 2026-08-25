#import <UIKit/UIKit.h>

static const CGSize HigurashiMinimumWindowSize = { 640.0, 400.0 };

static void HigurashiApplyWindowRestrictions(UIScene *scene)
{
    if (@available(iOS 13.0, *) && [scene isKindOfClass:[UIWindowScene class]])
    {
        UIWindowScene *windowScene = (UIWindowScene *)scene;
        UISceneSizeRestrictions *restrictions = windowScene.sizeRestrictions;
        if (restrictions != nil)
        {
            restrictions.minimumSize = HigurashiMinimumWindowSize;
        }
    }
}

@interface HigurashiWindowRestrictionsObserver : NSObject
@end

@implementation HigurashiWindowRestrictionsObserver

+ (void)load
{
    if (@available(iOS 13.0, *))
    {
        NSNotificationCenter *center = NSNotificationCenter.defaultCenter;
        [center addObserver:self
                   selector:@selector(higurashi_sceneConnected:)
                       name:UISceneDidConnectNotification
                     object:nil];
        [center addObserver:self
                   selector:@selector(higurashi_sceneActivated:)
                       name:UISceneDidActivateNotification
                     object:nil];
    }
}

+ (void)higurashi_sceneConnected:(NSNotification *)notification
{
    HigurashiApplyWindowRestrictions(notification.object);
}

+ (void)higurashi_sceneActivated:(NSNotification *)notification
{
    HigurashiApplyWindowRestrictions(notification.object);
}

@end
