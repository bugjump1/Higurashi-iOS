#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

static UIViewController *HigurashiDiagnosticTopViewController(void)
{
    UIWindow *window = nil;
    for (UIScene *scene in UIApplication.sharedApplication.connectedScenes)
    {
        if (scene.activationState != UISceneActivationStateUnattached &&
            [scene isKindOfClass:[UIWindowScene class]])
        {
            for (UIWindow *candidate in ((UIWindowScene *)scene).windows)
            {
                if (candidate.isKeyWindow)
                {
                    window = candidate;
                    break;
                }
                if (window == nil) window = candidate;
            }
        }
        if (window != nil) break;
    }

    UIViewController *controller = window.rootViewController;
    while (controller.presentedViewController != nil)
    {
        controller = controller.presentedViewController;
    }
    return controller;
}

extern "C" int Higurashi_ShareDiagnosticLog(const char *filePath)
{
    NSString *path = [NSString stringWithUTF8String:filePath ?: ""];
    if (path.length == 0 || ![NSFileManager.defaultManager fileExistsAtPath:path])
    {
        return 0;
    }

    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController *presenter = HigurashiDiagnosticTopViewController();
        if (presenter == nil) return;

        NSURL *url = [NSURL fileURLWithPath:path];
        UIActivityViewController *activity =
            [[UIActivityViewController alloc] initWithActivityItems:@[url]
                                              applicationActivities:nil];
        activity.modalPresentationStyle = UIModalPresentationFormSheet;
        UIPopoverPresentationController *popover = activity.popoverPresentationController;
        if (popover != nil)
        {
            popover.sourceView = presenter.view;
            popover.sourceRect = CGRectMake(CGRectGetMidX(presenter.view.bounds),
                                            CGRectGetMidY(presenter.view.bounds), 1, 1);
            popover.permittedArrowDirections = 0;
        }
        [presenter presentViewController:activity animated:YES completion:nil];
    });
    return 1;
}
